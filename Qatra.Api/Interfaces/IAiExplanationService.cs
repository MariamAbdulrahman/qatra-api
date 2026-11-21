// namespace Qatra.Api.Interfaces
// {
//     public class IAiExplanationService
//     {
//     }
// }

using Qatra.Api.Models.Enums;

namespace Qatra.Api.Interfaces;

public interface IAiExplanationService
{
    Task<ExplanationResult> ExplainAsync(ExplanationInput input, CancellationToken ct = default);
}

public record ExplanationInput(
    string NeighborhoodNameAr,
    IssueType IssueType,
    int DistinctDevices,
    int? MaxDurationDays,
    bool IsRepeat,
    bool UrgentHealth,
    int? Rank,
    double? CycleMean,
    double? ProbArrival2d,
    bool? AbnormalDelay,
    string? PredictionBasis);

public record ExplanationResult(string Text, bool FromModel, string? Error);