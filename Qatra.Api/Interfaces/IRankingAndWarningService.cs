using Qatra.Api.Models.Enums;

namespace Qatra.Api.Interfaces;

/// <summary>نتيجة ترتيب حادثة واحدة مع درجتها وتفسيرها.</summary>
public record RankedIncident(
    int IncidentId,
    int NeighborhoodId,
    string NeighborhoodNameAr,
    IssueType IssueType,
    IncidentStatus Status,
    int DistinctDevices,
    int? MaxDurationDays,
    bool IsRepeat,
    bool UrgentHealth,
    DateTime LastReportAt,
    DateTime CreatedAt,
    double Score,
    int Rank,
    string Explanation,
    bool ExplanationFromModel,
    PredictionResult? Prediction);

public interface IRankingAndWarningService
{
    /// <summary>يُرجع الحوادث المفتوحة مرتّبةً تنازلياً بالدرجة مع التفسير والتنبؤ.</summary>
    Task<List<RankedIncident>> GetRankedIncidentsAsync(CancellationToken ct = default);
}
