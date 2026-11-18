using System.ComponentModel.DataAnnotations;

namespace Qatra.Api.DTOs;

public class CreateReportRequest
{
    [Required, StringLength(200, MinimumLength = 8)]
    public string DeviceKey { get; set; } = string.Empty;

    [Required]
    public int NeighborhoodId { get; set; }

    [StringLength(1000)]
    public string? Text { get; set; }

    /// اختياري: outage أو tanker_delay أو contaminated أو other
    public string? QuickButton { get; set; }

    public bool WaterArrived { get; set; }
}