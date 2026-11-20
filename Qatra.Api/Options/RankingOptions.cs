namespace Qatra.Api.Options;

public class RankingOptions
{
    public const string SectionName = "Ranking";

    public double W1Volume   { get; set; } = 0.35;
    public double W2Duration { get; set; } = 0.30;
    public double W3Recency  { get; set; } = 0.20;
    public double W4Severity { get; set; } = 0.15;

    /// النافذة الزمنية لحساب الحداثة — تتصفّر recency عند تجاوزها
    public double RecencyWindowHours { get; set; } = 72.0;
}
