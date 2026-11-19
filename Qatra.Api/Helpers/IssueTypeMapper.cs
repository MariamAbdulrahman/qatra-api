using Qatra.Api.Models.Enums;

namespace Qatra.Api.Helpers;

public static class IssueTypeMapper
{
    public static IssueType? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "outage" => IssueType.Outage,
        "contaminated" => IssueType.Contaminated,
        _ => null
    };
}