using Qatra.Api.Models.Enums;

namespace Qatra.Api.Interfaces;

public interface IAiExtractionService
{
    /// <summary>
    /// يستخرج بيانات البلاغ من نص المواطن. لا يرمي استثناء: يرجع Success=false مع سبب.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(string rawText, CancellationToken ct = default);
}

/// <summary>
/// الحقول تطابق أعمدة Report التي يملؤها الاستخراج:
/// IssueType, DurationDays, EventDate, ExtractionConfidence.
/// Retryable = true عند أخطاء مؤقتة (شبكة، 429، 5xx) ليعيد الـ Worker المحاولة.
/// </summary>
public record ExtractionResult(
    bool Success,
    IssueType? IssueType,
    int? DurationDays,
    DateTime? EventDate,
    double? Confidence,
    string? Error,
    bool Retryable)
{
    public static ExtractionResult Ok(IssueType issueType, int? durationDays, DateTime? eventDate, double confidence)
        => new(true, issueType, durationDays, eventDate, confidence, null, false);

    public static ExtractionResult Fail(string error, bool retryable = false)
        => new(false, null, null, null, null, error, retryable);
}