namespace Qatra.Api.Models;

public class Prediction
{
    public int Id { get; set; }

    public int NeighborhoodId { get; set; }
    public Neighborhood Neighborhood { get; set; } = null!;

    public string ModelVersion { get; set; } = string.Empty;

    public string Inputs { get; set; } = string.Empty;

    public string Outputs { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}