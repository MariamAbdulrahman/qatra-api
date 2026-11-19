using System.ComponentModel.DataAnnotations;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.DTOs;

public class CreateReportDto
{
    [Range(1, int.MaxValue)]
    public int NeighborhoodId { get; set; }

    // اختياري: المواطن قد يكتفي بالزر
    [StringLength(500)]
    public string? Text { get; set; }

    // إجباري: outage أو contaminated
    [Required(ErrorMessage = "يجب اختيار نوع المشكلة (انقطاع ماء أو تلوث ماء).")]
    public IssueType? QuickType { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string DeviceKey { get; set; } = string.Empty;
}