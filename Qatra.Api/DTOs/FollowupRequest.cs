using System.ComponentModel.DataAnnotations;

namespace Qatra.Api.DTOs;

public class FollowupRequest
{
    [Required, StringLength(200, MinimumLength = 8)]
    public string DeviceKey { get; set; } = string.Empty;

    [Range(0, 60)]
    public int DurationDays { get; set; }
}