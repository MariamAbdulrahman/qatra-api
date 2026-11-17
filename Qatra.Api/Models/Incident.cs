using Qatra.Api.Models.Enums;

namespace Qatra.Api.Models;

public class Incident
{
    public int Id { get; set; }

    public int NeighborhoodId { get; set; }
    public Neighborhood Neighborhood { get; set; } = null!;

    public IssueType IssueType { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.New;

    public int? MaxDurationDays { get; set; }

    public int DistinctDevices { get; set; }

    public bool IsRepeat { get; set; }

    public string? Explanation { get; set; }

    public DateTime? ActualArrivalDate { get; set; }

    public bool? WasAbnormal { get; set; }

    public DateTime LastReportAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}