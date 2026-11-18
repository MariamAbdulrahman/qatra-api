namespace Qatra.Api.Options;

public class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// أقصى عدد بلاغات للجهاز الواحد في اليوم
    public int DailyReportLimit { get; set; } = 5;
}