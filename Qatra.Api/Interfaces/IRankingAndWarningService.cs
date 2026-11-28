using Qatra.Api.Models.Enums;

namespace Qatra.Api.Interfaces;

/// <summary>ملخص بلاغ مرتبط بحادثة لعرضه في لوحة التحكم.</summary>
public record IncidentReportDto(
    int Id,
    string RawText,
    IssueType? IssueType,
    int? DurationDays,
    DateTime CreatedAt);

/// <summary>نتيجة ترتيب حادثة واحدة مع درجتها وتفسيرها وبلاغاتها المرتبطة.</summary>
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
    PredictionResult? Prediction,
    List<IncidentReportDto> Reports);

public interface IRankingAndWarningService
{
    /// <summary>يُرجع الحوادث المفتوحة مرتّبةً بالقاعدة الذهبية ثم الدرجة مع التفسير والتنبؤ والبلاغات.</summary>
    Task<List<RankedIncident>> GetRankedIncidentsAsync(CancellationToken ct = default);
}
