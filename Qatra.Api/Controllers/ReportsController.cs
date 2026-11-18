using Microsoft.AspNetCore.Mvc;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportIngestionService _ingestion;

    public ReportsController(IReportIngestionService ingestion)
    {
        _ingestion = ingestion;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateReportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken ct)
    {
        var result = await _ingestion.CreateAsync(request, ct);

        return result.Outcome switch
        {
            IngestionOutcome.Created =>
                Created($"/api/reports/{result.ReportId}",
                    new CreateReportResponse(result.ReportId!.Value, result.NeedsFollowup)),

            IngestionOutcome.NeighborhoodNotFound =>
                NotFound(new ProblemDetails { Title = "الحي غير موجود", Status = 404 }),

            IngestionOutcome.DailyLimitExceeded =>
                StatusCode(StatusCodes.Status429TooManyRequests,
                    new ProblemDetails { Title = "تجاوزتَ الحد اليومي للبلاغات", Status = 429 }),

            IngestionOutcome.InvalidQuickButton =>
                BadRequest(new ProblemDetails
                {
                    Title = "قيمة الزر السريع غير صالحة",
                    Detail = "القيم المسموحة: outage, tanker_delay, contaminated, other",
                    Status = 400
                }),

            IngestionOutcome.EmptyReport =>
                BadRequest(new ProblemDetails
                {
                    Title = "البلاغ فارغ",
                    Detail = "أرسل نصاً أو زراً سريعاً أو WaterArrived = true",
                    Status = 400
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:int}/followup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Followup(int id, [FromBody] FollowupRequest request, CancellationToken ct)
    {
        var outcome = await _ingestion.AddFollowupAsync(id, request, ct);

        return outcome switch
        {
            FollowupOutcome.Updated => NoContent(),
            FollowupOutcome.NotFound => NotFound(),
            FollowupOutcome.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}