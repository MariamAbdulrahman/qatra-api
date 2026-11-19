using Qatra.Api.Models.Enums;

namespace Qatra.Api.DTOs;

public class ReportCreatedDto
{
    public int Id { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}