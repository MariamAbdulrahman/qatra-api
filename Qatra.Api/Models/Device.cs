namespace Qatra.Api.Models;

public class Device
{
    public int Id { get; set; }

    public string DeviceKeyHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}