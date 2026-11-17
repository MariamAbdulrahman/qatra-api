namespace Qatra.Api.Models;

public class WaterSupplyEvent
{
    public int Id { get; set; }

    public int NeighborhoodId { get; set; }
    public Neighborhood Neighborhood { get; set; } = null!;

    public DateTime EventDate { get; set; }

    public int ReportCount { get; set; }
}