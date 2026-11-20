namespace Qatra.Api.Options;

public class IncidentsOptions
{
    public const string SectionName = "Incidents";

    /// نافذة الدمج: بلاغ لنفس الحي/النوع ضمن هذه الساعات يُضم لحادثة مفتوحة
    public int MergeWindowHours { get; set; } = 72;

    /// نافذة التكرار: حادثة Resolved خلال هذه الساعات تجعل IsRepeat = true
    public int RepeatWindowHours { get; set; } = 72;

    /// حادثة لم تتلقَّ بلاغاً منذ هذه الساعات تُغلق تلقائياً بحالة Expired
    public int ExpirationHours { get; set; } = 72;
}
