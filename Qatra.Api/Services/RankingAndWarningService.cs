using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Models.Enums;
using Qatra.Api.Options;

namespace Qatra.Api.Services;

public class RankingAndWarningService : IRankingAndWarningService
{
    private readonly AppDbContext _db;
    private readonly RankingOptions _opts;
    private readonly IPredictionService _predictions;
    private readonly IAiExplanationService _explanation;

    public RankingAndWarningService(
        AppDbContext db,
        IOptions<RankingOptions> opts,
        IPredictionService predictions,
        IAiExplanationService explanation)
    {
        _db = db;
        _opts = opts.Value;
        _predictions = predictions;
        _explanation = explanation;
    }

    public async Task<List<RankedIncident>> GetRankedIncidentsAsync(CancellationToken ct = default)
    {
        var openStatuses = new[] { IncidentStatus.New, IncidentStatus.Confirmed, IncidentStatus.InProgress };

        var incidents = await _db.Incidents
            .AsNoTracking()
            .Include(i => i.Neighborhood)
            .Include(i => i.Reports)
            .Where(i => openStatuses.Contains(i.Status))
            .ToListAsync(ct);

        if (incidents.Count == 0) return [];

        var now = DateTime.UtcNow;
        var scored = incidents.Select(i =>
        {
            var effectiveDuration = ComputeEffectiveDurationDays(i, now);
            var score = ComputeScore(i, effectiveDuration, now);
            return (Incident: i, EffectiveDuration: effectiveDuration, Score: score);
        }).ToList();

        // القاعدة الذهبية: حالات الخطر الصحي العاجل تتصدر القائمة دائماً، ثم ترتيب تنازلي بالدرجة
        scored.Sort((a, b) =>
        {
            int urgentCmp = b.Incident.UrgentHealth.CompareTo(a.Incident.UrgentHealth);
            if (urgentCmp != 0) return urgentCmp;
            int scoreCmp = b.Score.CompareTo(a.Score);
            if (scoreCmp != 0) return scoreCmp;
            return b.Incident.DistinctDevices.CompareTo(a.Incident.DistinctDevices);
        });

        var result = new List<RankedIncident>(scored.Count);
        for (int i = 0; i < scored.Count; i++)
        {
            result.Add(await BuildRankedIncidentAsync(
                scored[i].Incident,
                scored[i].EffectiveDuration,
                scored[i].Score,
                i + 1,
                ct));
        }

        return result;
    }

    // ----------------------------------------------------------------
    // بناء RankedIncident مع التنبؤ والتفسير والبلاغات المرتبطة
    // ----------------------------------------------------------------
    private async Task<RankedIncident> BuildRankedIncidentAsync(
        Models.Incident incident,
        int? effectiveDurationDays,
        double score,
        int rank,
        CancellationToken ct)
    {
        var prediction = await _predictions.GetPredictionAsync(incident.NeighborhoodId, ct);

        double? cycleMean = null;
        double? probArrival2d = null;
        bool? abnormalDelay = null;
        string? predictionBasis = null;

        if (prediction is not null)
        {
            var d = prediction.Details;
            cycleMean       = TryGetDouble(d, "cycleMean")      ?? TryGetDouble(d, "cycle_days");
            probArrival2d   = TryGetDouble(d, "probArrival2d")  ?? TryGetDouble(d, "prob_arrival_2d");
            abnormalDelay   = TryGetBool(d, "abnormalDelay")    ?? TryGetBool(d, "abnormal_delay");
            predictionBasis = TryGetString(d, "basis")          ?? prediction.Source;
        }

        var input = new ExplanationInput(
            incident.Neighborhood.NameAr, incident.IssueType,
            incident.DistinctDevices, effectiveDurationDays,
            incident.IsRepeat, incident.UrgentHealth,
            rank, cycleMean, probArrival2d, abnormalDelay, predictionBasis);

        var expl = await _explanation.ExplainAsync(input, ct);

        var reports = incident.Reports
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new IncidentReportDto(
                r.Id,
                r.RawText,
                r.IssueType ?? r.QuickButton,
                r.DurationDays,
                AsUtc(r.CreatedAt)))
            .ToList();

        return new RankedIncident(
            incident.Id,
            incident.NeighborhoodId,
            incident.Neighborhood.NameAr,
            incident.IssueType,
            incident.Status,
            incident.DistinctDevices,
            effectiveDurationDays,
            incident.IsRepeat,
            incident.UrgentHealth,
            AsUtc(incident.LastReportAt),
            AsUtc(incident.CreatedAt),
            Math.Round(score, 2),
            rank,
            expl.Text,
            expl.FromModel,
            prediction,
            reports);
    }

    // ----------------------------------------------------------------
    // المدة الفعلية للانقطاع = المدة المبلَّغ عنها + الأيام المنقضية منذ آخر بلاغ
    // ----------------------------------------------------------------
    private static int? ComputeEffectiveDurationDays(Models.Incident incident, DateTime now)
    {
        if (!incident.MaxDurationDays.HasValue) return null;
        var elapsedDays = (int)Math.Floor(Math.Max(0, (now - AsUtc(incident.LastReportAt)).TotalDays));
        return incident.MaxDurationDays.Value + elapsedDays;
    }

    // ----------------------------------------------------------------
    // score = 100 * (w1*vol + w2*dur + w3*rec + w4*sev)
    // ----------------------------------------------------------------
    private double ComputeScore(Models.Incident incident, int? effectiveDurationDays, DateTime now)
    {
        double volume   = Math.Min(incident.DistinctDevices / 10.0, 1.0);
        double duration = effectiveDurationDays.HasValue
            ? Math.Min(effectiveDurationDays.Value / 7.0, 1.0) : 0.0;
        double hoursSince = Math.Max(0, (now - AsUtc(incident.LastReportAt)).TotalHours);
        // حد أدنى 0.35 للحداثة حتى لا تهبط الحوادث غير المحلولة لمجرد توقف التكرار
        double recency  = Math.Max(1.0 - hoursSince / _opts.RecencyWindowHours, 0.35);
        double severity = incident.IssueType switch
        {
            IssueType.Contaminated => 1.0,
            IssueType.Outage       => 0.8,
            _                      => 0.3
        };
        return 100.0 * (_opts.W1Volume * volume + _opts.W2Duration * duration
                      + _opts.W3Recency * recency + _opts.W4Severity * severity);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ----------------------------------------------------------------
    // مساعدات JsonElement
    // ----------------------------------------------------------------
    private static double? TryGetDouble(System.Text.Json.JsonElement el, string key)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Object &&
            el.TryGetProperty(key, out var p) && p.TryGetDouble(out var v)) return v;
        return null;
    }

    private static bool? TryGetBool(System.Text.Json.JsonElement el, string key)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Object &&
            el.TryGetProperty(key, out var p))
        {
            if (p.ValueKind == System.Text.Json.JsonValueKind.True)  return true;
            if (p.ValueKind == System.Text.Json.JsonValueKind.False) return false;
        }
        return null;
    }

    private static string? TryGetString(System.Text.Json.JsonElement el, string key)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Object &&
            el.TryGetProperty(key, out var p) &&
            p.ValueKind == System.Text.Json.JsonValueKind.String) return p.GetString();
        return null;
    }
}
