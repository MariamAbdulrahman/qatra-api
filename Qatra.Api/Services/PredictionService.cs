using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Models;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Services;

public class PredictionService : IPredictionService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly ILogger<PredictionService> _logger;
    private readonly string _predictPath;
    private readonly int _cacheMinutes;

    public PredictionService(
        HttpClient http,
        AppDbContext db,
        IConfiguration config,
        ILogger<PredictionService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
        _predictPath = config["PredictionService:PredictPath"] ?? "/predict";
        _cacheMinutes = config.GetValue("PredictionService:CacheMinutes", 30);
    }

    // عقد خدمة Python حسب https://qatra-ml-service.onrender.com/docs
    private record MlRequest(
        double PriorMean,
        double PriorSd,
        List<double> Intervals,
        double DaysSince,
        int ActiveDevices,
        int ReportsLast24h);

    public async Task<PredictionResult?> GetPredictionAsync(int neighborhoodId, CancellationToken ct = default)
    {
        var hood = await _db.Neighborhoods.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == neighborhoodId, ct);
        if (hood is null) return null;

        var now = DateTime.UtcNow;

        // 1) جمع الفواصل التاريخية لوصول الماء من WaterSupplyEvents (إن وُجدت)
        var supplyDates = await _db.WaterSupplyEvents.AsNoTracking()
            .Where(e => e.NeighborhoodId == hood.Id)
            .OrderBy(e => e.EventDate)
            .Select(e => e.EventDate)
            .ToListAsync(ct);

        var intervals = new List<double>();
        for (int i = 1; i < supplyDates.Count; i++)
        {
            var prev = DateTime.SpecifyKind(supplyDates[i - 1], DateTimeKind.Utc);
            var curr = DateTime.SpecifyKind(supplyDates[i], DateTimeKind.Utc);
            var diffDays = (curr - prev).TotalDays;
            if (diffDays > 0.1)
                intervals.Add(Math.Round(diffDays, 2));
        }

        // 2) حساب daysSince الحقيقي من الحادثة المفتوحة للانقطاع أو آخر حدث وصول ماء
        var openStatuses = new[] { IncidentStatus.New, IncidentStatus.Confirmed, IncidentStatus.InProgress };
        var openOutage = await _db.Incidents.AsNoTracking()
            .Where(i => i.NeighborhoodId == hood.Id
                     && i.IssueType == IssueType.Outage
                     && openStatuses.Contains(i.Status))
            .OrderByDescending(i => i.LastReportAt)
            .FirstOrDefaultAsync(ct);

        double daysSince = 0;
        DateTime? openOutageLastReportUtc = null;
        if (openOutage is not null)
        {
            openOutageLastReportUtc = DateTime.SpecifyKind(openOutage.LastReportAt, DateTimeKind.Utc);
            var createdUtc = DateTime.SpecifyKind(openOutage.CreatedAt, DateTimeKind.Utc);

            if (openOutage.MaxDurationDays.HasValue && openOutage.MaxDurationDays.Value > 0)
            {
                var elapsedSinceLastReport = Math.Max(0, (now - openOutageLastReportUtc.Value).TotalDays);
                daysSince = openOutage.MaxDurationDays.Value + elapsedSinceLastReport;
            }
            else
            {
                daysSince = Math.Max(0, (now - createdUtc).TotalDays);
            }
        }
        else if (supplyDates.Count > 0)
        {
            var lastSupplyUtc = DateTime.SpecifyKind(supplyDates[^1], DateTimeKind.Utc);
            daysSince = Math.Max(0, (now - lastSupplyUtc).TotalDays);
        }
        daysSince = Math.Round(daysSince, 1);

        // 3) من الكاش (جدول Predictions) فقط إذا لم تتغير بيانات الأزمة أو daysSince بشكل مؤثر
        var cutoff = now.AddMinutes(-_cacheMinutes);
        var cached = await _db.Predictions.AsNoTracking()
            .Where(p => p.NeighborhoodId == neighborhoodId && p.CreatedAt >= cutoff)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (cached is not null && TryParse(cached.Outputs, out var cachedDetails))
        {
            var cachedCreatedUtc = DateTime.SpecifyKind(cached.CreatedAt, DateTimeKind.Utc);
            bool fresherThanIncident = openOutageLastReportUtc is null || cachedCreatedUtc >= openOutageLastReportUtc.Value;
            bool sameDaysSince = TryGetCachedDaysSince(cached.Inputs, out var cachedDays)
                                 && Math.Abs(cachedDays - daysSince) < 0.5;

            if (fresherThanIncident && sameDaysSince)
            {
                return new PredictionResult(
                    neighborhoodId,
                    "cache",
                    cached.ModelVersion,
                    cachedCreatedUtc,
                    cachedDetails);
            }
        }

        // 4) من خدمة Python ML
        var since = now.AddHours(-24);
        var recent = _db.Reports.AsNoTracking()
            .Where(r => r.NeighborhoodId == hood.Id && r.CreatedAt >= since);

        var reportsLast24h = await recent.CountAsync(ct);
        var activeDevices = await recent.Select(r => r.DeviceId).Distinct().CountAsync(ct);
        if (openOutage is not null && openOutage.DistinctDevices > activeDevices)
            activeDevices = openOutage.DistinctDevices;

        var request = new MlRequest(
            hood.PriorCycleDays,
            hood.PriorCycleSd,
            intervals,
            daysSince,
            activeDevices,
            reportsLast24h);

        try
        {
            using var response = await _http.PostAsJsonAsync(_predictPath, request, JsonOpts, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!TryParse(raw, out var details))
                throw new JsonException("Empty or invalid JSON from prediction service.");

            string version = "unknown";
            if (details.ValueKind == JsonValueKind.Object)
            {
                if (details.TryGetProperty("modelVersion", out var v1) && v1.ValueKind == JsonValueKind.String)
                    version = v1.GetString()!;
                else if (details.TryGetProperty("model_version", out var v2) && v2.ValueKind == JsonValueKind.String)
                    version = v2.GetString()!;
            }

            var entity = new Prediction
            {
                NeighborhoodId = hood.Id,
                ModelVersion = version,
                Inputs = JsonSerializer.Serialize(request, JsonOpts),
                Outputs = raw,
                CreatedAt = DateTime.UtcNow
            };
            _db.Predictions.Add(entity);
            await _db.SaveChangesAsync(ct);

            return new PredictionResult(
                hood.Id,
                "model",
                version,
                DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc),
                details);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // 5) احتياطي متوافق مع نفس عقد JSON: لا نخزّنه، ليُعاد المحاولة مع أول طلب لاحق
            _logger.LogWarning(ex, "Prediction service failed for neighborhood {Id}; using prior fallback.", hood.Id);

            var margin = 1.645 * hood.PriorCycleSd;
            var ciLow = Math.Round(Math.Max(1.0, hood.PriorCycleDays - margin), 1);
            var ciHigh = Math.Round(hood.PriorCycleDays + margin, 1);
            var isAbnormalFallback = daysSince > ciHigh;

            var fallback = JsonSerializer.SerializeToElement(new
            {
                cycleMean = Math.Round(hood.PriorCycleDays, 1),
                ci90 = new[] { ciLow, ciHigh },
                probArrival2d = (double?)null,
                probLate = isAbnormalFallback ? 1.0 : (double?)null,
                abnormalDelay = isAbnormalFallback,
                basis = "0 observations (Prior only)"
            });

            return new PredictionResult(hood.Id, "prior_fallback", "prior", DateTime.UtcNow, fallback);
        }
    }

    private static bool TryGetCachedDaysSince(string inputsJson, out double daysSince)
    {
        daysSince = 0;
        if (!TryParse(inputsJson, out var el) || el.ValueKind != JsonValueKind.Object)
            return false;

        if (el.TryGetProperty("daysSince", out var prop) && prop.TryGetDouble(out var value))
        {
            daysSince = value;
            return true;
        }
        return false;
    }

    private static bool TryParse(string json, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
