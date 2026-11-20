using System.ComponentModel.DataAnnotations;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.DTOs;

public record UpdateIncidentStatusRequest(
    [Required] IncidentStatus Status
);
