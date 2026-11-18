using Qatra.Api.Models.Enums;

namespace Qatra.Api.Helpers;

public static class IssueTypeMapper
{
    public static IssueType? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "outage" => IssueType.Outage,
        "tanker_delay" => IssueType.TankerDelay,
        "contaminated" => IssueType.Contaminated,
        "other" => IssueType.Other,
        _ => null
    };
}