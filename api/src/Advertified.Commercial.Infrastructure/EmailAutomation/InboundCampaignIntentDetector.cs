using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public static partial class InboundCampaignIntentDetector
{
    public static bool ContainsMultipleExplicitBriefs(string content) =>
        BriefHeading().Matches(content)
            .Select(match => match.Groups["number"].Value)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .Count() > 1;

    [GeneratedRegex(
        @"(?im)^\s*\*{0,2}\s*brief\s*#?\s*(?<number>\d+)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex BriefHeading();
}
