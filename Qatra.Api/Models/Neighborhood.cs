namespace Qatra.Api.Models;

public class Neighborhood
{
    public int Id { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public double Lat { get; set; }

    public double Lng { get; set; }

    public int Population { get; set; }

    public double PriorCycleDays { get; set; }

    public double PriorCycleSd { get; set; } = 1.5;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<WaterSupplyEvent> WaterSupplyEvents { get; set; } = new List<WaterSupplyEvent>();
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}