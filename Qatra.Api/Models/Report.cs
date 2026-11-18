using Qatra.Api.Models.Enums;

namespace Qatra.Api.Models;

public class Report
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int NeighborhoodId { get; set; }
    public Neighborhood Neighborhood { get; set; } = null!;

    public int? IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public string RawText { get; set; } = string.Empty;

    public IssueType? QuickButton { get; set; }

    public IssueType? IssueType { get; set; }

    public int? DurationDays { get; set; }

    public DateTime? EventDate { get; set; }

    public bool WaterArrived { get; set; }

    public bool NeedsFollowup { get; set; }

    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;

    public double? ExtractionConfidence { get; set; }

    public IssueType? HumanLabel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}