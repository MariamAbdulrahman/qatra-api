// using Qatra.Api.Models.Enums;

// namespace Qatra.Api.Interfaces;

// public interface IAiExplanationService
// {
//     /// <summary>يولّد جملتي التبرير للمدير. لا يرمي استثناء: عند الفشل يرجع نص القالب.</summary>
//     Task<ExplanationResult> ExplainAsync(ExplanationInput input, CancellationToken ct = default);
// }

// /// <summary>كل الأرقام يحسبها الكود، والنموذج يصوغها فقط.</summary>
// public record ExplanationInput(
//     string NeighborhoodNameAr,
//     IssueType IssueType,
//     int DistinctDevices,
//     int? MaxDurationDays,
//     bool IsRepeat,
//     bool UrgentHealth,
//     int? Rank,                  // ترتيب الحي بين الأحياء (من خدمة الأولوية)
//     double? CycleMean,          // من التنبؤ: details.cycleMean
//     double? ProbArrival2d,      // من التنبؤ: details.probArrival2d (0 إلى 1)
//     bool? AbnormalDelay,        // من التنبؤ: details.abnormalDelay
//     string? PredictionBasis);   // من التنبؤ: details.basis

// /// <summary>FromModel=false يعني أن النص من القالب الثابت (Error يوضح السبب).</summary>
// public record ExplanationResult(string Text, bool FromModel, string? Error);


using Qatra.Api.Models.Enums;

namespace Qatra.Api.Interfaces;

public interface IAiExtractionService
{
    Task<ExtractionResult> ExtractAsync(string? rawText, CancellationToken ct = default);
}

public record ExtractionResult(
    bool Success,
    IssueType? IssueType,
    int? DurationDays,
    DateTime? EventDate,
    double? Confidence,
    string? Error,
    bool Retryable = false);