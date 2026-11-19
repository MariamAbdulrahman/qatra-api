using Qatra.Api.Models.Enums;

namespace Qatra.Api.DTOs;

public class ReportStatusDto
{
    public int Id { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public bool NeedsFollowup { get; set; }
    public int RelatedReports { get; set; }
}