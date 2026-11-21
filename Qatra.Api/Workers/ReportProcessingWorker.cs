using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Models;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Workers;

public class ReportProcessingWorker : BackgroundService
{
    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReportProcessingWorker> _logger;
    private readonly TimeSpan _poll;
    private readonly TimeSpan _gap;
    private readonly int _batchSize;

    // بلاغات فشل دمجها أو لم تُدمج: نتجاهلها فترة لئلا ندور عليها بلا توقف
    private readonly ConcurrentDictionary<int, DateTime> _skipUntil = new();
    private int _retryableStreak;

    public ReportProcessingWorker(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<ReportProcessingWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _poll = TimeSpan.FromSeconds(config.GetValue("ReportProcessing:PollSeconds", 15));
        _gap = TimeSpan.FromSeconds(config.GetValue("ReportProcessing:DelayBetweenReportsSeconds", 15));
        _batchSize = config.GetValue("ReportProcessing:BatchSize", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = _poll;
            try
            {
                var pause = await RunBatchAsync(stoppingToken);
                if (pause is not null) wait = pause.Value;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report processing cycle failed.");
            }

            try { await Task.Delay(wait, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ------------------------------------------------------------------ الدفعة
    private async Task<TimeSpan?> RunBatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var item in _skipUntil.Where(x => x.Value <= now).ToList())
            _skipUntil.TryRemove(item.Key, out _);
        var skip = _skipUntil.Keys.ToList();

        List<int> ids;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await db.Reports.AsNoTracking()
                .Where(r => (r.ProcessingStatus == ProcessingStatus.Pending
                             || (r.ProcessingStatus == ProcessingStatus.Processed && r.IncidentId == null))
                            && !skip.Contains(r.Id))
                .OrderBy(r => r.CreatedAt)
                .Select(r => r.Id)
                .Take(_batchSize)
                .ToListAsync(ct);
        }

        for (var i = 0; i < ids.Count; i++)
        {
            if (i > 0) await Task.Delay(_gap, ct);
            var pause = await ProcessOneAsync(ids[i], ct);
            if (pause is not null) return pause;   // خطأ مؤقت: نوقف الدفعة كلها
        }
        return null;
    }

    // ------------------------------------------------------------------ بلاغ واحد
    private async Task<TimeSpan?> ProcessOneAsync(int reportId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<AppDbContext>();

            var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null) return null;

            if (report.ProcessingStatus == ProcessingStatus.Pending)
            {
                var pause = await ExtractAsync(db, sp.GetRequiredService<IAiExtractionService>(), report, ct);
                if (pause is not null) return pause;
                if (report.ProcessingStatus != ProcessingStatus.Processed) return null;
            }

            await sp.GetRequiredService<IIncidentMergingService>().MergeAsync(report, ct);

            // لم يُدمج البلاغ (لا يوجد نوع): نتجاهله 30 دقيقة بدل أن يُلتقط في كل دورة
            if (report.IncidentId is null)
            {
                _logger.LogWarning("Report {Id} was not merged (no issue type); skipping for 30 minutes.", reportId);
                _skipUntil[reportId] = DateTime.UtcNow.AddMinutes(30);
                return null;
            }

            try
            {
                await ExplainAsync(db,
                    sp.GetRequiredService<IPredictionService>(),
                    sp.GetRequiredService<IAiExplanationService>(),
                    report, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Explanation step failed for report {Id}.", reportId);
            }

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing report {Id} failed; retrying in 5 minutes.", reportId);
            _skipUntil[reportId] = DateTime.UtcNow.AddMinutes(5);
            return null;
        }
    }

    // ------------------------------------------------------------------ الاستخراج
    private async Task<TimeSpan?> ExtractAsync(
        AppDbContext db, IAiExtractionService extractor, Report report, CancellationToken ct)
    {
        var result = await extractor.ExtractAsync(report.RawText, ct);

        if (result.Success)
        {
            Interlocked.Exchange(ref _retryableStreak, 0);
            report.IssueType = result.IssueType;
            report.DurationDays = result.DurationDays;
            report.EventDate = result.EventDate;
            report.ExtractionConfidence = result.Confidence;
            report.ProcessingStatus = ProcessingStatus.Processed;
            await db.SaveChangesAsync(ct);
            return null;
        }

        if (result.Retryable)
        {
            var n = Interlocked.Increment(ref _retryableStreak);
            var pause = Backoff[Math.Min(n, Backoff.Length) - 1];
            _logger.LogWarning("Extraction temporarily failed ({Error}); pausing {Pause}. Report {Id} stays Pending.",
                result.Error, pause, report.Id);
            return pause;
        }

        // خطأ دائم: الزر السريع إجباري، فنعتمده نوعاً للبلاغ
        if (report.QuickButton is not null)
        {
            report.IssueType = report.QuickButton;
            report.ProcessingStatus = ProcessingStatus.Processed;
            _logger.LogWarning("Extraction failed for report {Id} ({Error}); using QuickButton.",
                report.Id, result.Error);
        }
        else
        {
            report.ProcessingStatus = ProcessingStatus.Failed;
            _logger.LogError("Extraction failed for report {Id} ({Error}); marked Failed.",
                report.Id, result.Error);
        }
        await db.SaveChangesAsync(ct);
        return null;
    }

    // ------------------------------------------------------------------ التبرير
    private async Task ExplainAsync(
        AppDbContext db, IPredictionService predictor, IAiExplanationService explainer,
        Report report, CancellationToken ct)
    {
        if (report.IncidentId is null) return;

        var incident = await db.Incidents.Include(i => i.Neighborhood)
            .FirstOrDefaultAsync(i => i.Id == report.IncidentId, ct);
        if (incident is null || !string.IsNullOrWhiteSpace(incident.Explanation)) return;

        double? cycleMean = null, prob = null;
        bool? abnormal = null;
        string? basis = null;

        var prediction = await predictor.GetPredictionAsync(incident.NeighborhoodId, ct);
        if (prediction is not null && prediction.Details.ValueKind == JsonValueKind.Object)
        {
            var d = prediction.Details;
            cycleMean = Num(d, "cycleMean") ?? Num(d, "cycle_days");   // الثاني: رد الاحتياطي
            prob = Num(d, "probArrival2d");
            abnormal = Bool(d, "abnormalDelay");
            basis = Str(d, "basis") ?? (prediction.Source == "prior_fallback" ? "Prior only" : null);
        }

        var input = new ExplanationInput(
            incident.Neighborhood.NameAr,
            incident.IssueType,
            incident.DistinctDevices,
            incident.MaxDurationDays,
            incident.IsRepeat,
            incident.UrgentHealth,
            null,                // Rank: من خدمة الأولوية عند زميلكِ (لم تصل بعد)
            cycleMean, prob, abnormal, basis);

        var explanation = await explainer.ExplainAsync(input, ct);
        incident.Explanation = explanation.Text;
        await db.SaveChangesAsync(ct);

        if (!explanation.FromModel)
            _logger.LogWarning("Incident {Id} got the template explanation ({Error}).", incident.Id, explanation.Error);
    }

    private static double? Num(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static bool? Bool(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean() : null;

    private static string? Str(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}