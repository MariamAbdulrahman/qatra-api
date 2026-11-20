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
            .Where(i => openStatuses.Contains(i.Status))
            .ToListAsync(ct);

        if (incidents.Count == 0) return [];

        var now = DateTime.UtcNow;
        var scored = incidents.Select(i => (Incident: i, Score: ComputeScore(i, now))).ToList();

        // ترتيب تنازلي بالدرجة، urgent يتقدم عند التساوي
        scored.Sort((a, b) =>
        {
            int cmp = b.Score.CompareTo(a.Score);
            if (cmp != 0) return cmp;
            return b.Incident.UrgentHealth.CompareTo(a.Incident.UrgentHealth);
        });

        var result = new List<RankedIncident>(scored.Count);
        for (int i = 0; i < scored.Count; i++)
            result.Add(await BuildRankedIncidentAsync(scored[i].Incident, scored[i].Score, i + 1, ct));

        return result;
    }

    // ----------------------------------------------------------------
    // بناء RankedIncident مع التنبؤ والتفسير
    // ----------------------------------------------------------------
    private async Task<RankedIncident> BuildRankedIncidentAsync(
        Models.Incident incident, double score, int rank, CancellationToken ct)
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
            incident.DistinctDevices, incident.MaxDurationDays,
            incident.IsRepeat, incident.UrgentHealth,
            rank, cycleMean, probArrival2d, abnormalDelay, predictionBasis);

        var expl = await _explanation.ExplainAsync(input, ct);

        return new RankedIncident(
            incident.Id, incident.NeighborhoodId, incident.Neighborhood.NameAr,
            incident.IssueType, incident.Status, incident.DistinctDevices,
            incident.MaxDurationDays, incident.IsRepeat, incident.UrgentHealth,
            incident.LastReportAt, incident.CreatedAt,
            Math.Round(score, 2), rank, expl.Text, expl.FromModel, prediction);
    }

    // ----------------------------------------------------------------
    // score = 100 * (w1*vol + w2*dur + w3*rec + w4*sev)
    // ----------------------------------------------------------------
    private double ComputeScore(Models.Incident incident, DateTime now)
    {
        double volume   = Math.Min(incident.DistinctDevices / 10.0, 1.0);
        double duration = incident.MaxDurationDays.HasValue
            ? Math.Min(incident.MaxDurationDays.Value / 7.0, 1.0) : 0.0;
        double hoursSince = (now - incident.LastReportAt).TotalHours;
        double recency  = Math.Max(1.0 - hoursSince / _opts.RecencyWindowHours, 0.0);
        double severity = incident.IssueType switch
        {
            IssueType.Contaminated => 1.0,
            IssueType.Outage       => 0.8,
            _                      => 0.3
        };
        return 100.0 * (_opts.W1Volume * volume + _opts.W2Duration * duration
                      + _opts.W3Recency * recency + _opts.W4Severity * severity);
    }

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
