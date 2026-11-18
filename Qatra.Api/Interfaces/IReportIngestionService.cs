using Qatra.Api.DTOs;

namespace Qatra.Api.Interfaces;

public enum IngestionOutcome
{
    Created,
    NeighborhoodNotFound,
    DailyLimitExceeded,
    InvalidQuickButton,
    EmptyReport
}

public enum FollowupOutcome
{
    Updated,
    NotFound,
    Forbidden
}

public record IngestionResult(IngestionOutcome Outcome, int? ReportId = null, bool NeedsFollowup = false);

public interface IReportIngestionService
{
    Task<IngestionResult> CreateAsync(CreateReportRequest request, CancellationToken ct = default);
    Task<FollowupOutcome> AddFollowupAsync(int reportId, FollowupRequest request, CancellationToken ct = default);
}