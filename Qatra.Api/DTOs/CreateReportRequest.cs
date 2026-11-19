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

    /// إجباري: outage أو contaminated
    [Required(ErrorMessage = "يجب اختيار نوع المشكلة (انقطاع ماء أو تلوث ماء).")]
    public string? QuickButton { get; set; }
}