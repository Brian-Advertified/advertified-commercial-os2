using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

internal static partial class CampaignTimingParser
{
    private static readonly CultureInfo[] Cultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en-ZA"),
        CultureInfo.GetCultureInfo("en-GB"),
    ];

    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "d MMMM yyyy",
        "dd MMMM yyyy",
        "d MMM yyyy",
        "dd MMM yyyy",
        "d/M/yyyy",
        "dd/MM/yyyy",
    ];

    internal static MediaRunningPeriodInput[] Parse(string timing)
    {
        var dates = DateToken().Matches(timing)
            .Select(match => ParseDate(match.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .Order()
            .ToArray();
        if (dates.Length < 2 || dates[^1] < dates[0])
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "The campaign requires a clear start and end date before a proposal can be sent.");
        }
        return [new MediaRunningPeriodInput(dates[0], dates[^1])];
    }

    private static DateOnly? ParseDate(string value)
    {
        foreach (var culture in Cultures)
        {
            if (DateOnly.TryParseExact(
                    value.Trim(),
                    Formats,
                    culture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var exact))
            {
                return exact;
            }
            if (DateOnly.TryParse(
                    value.Trim(), culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    [GeneratedRegex(
        @"\b(?:\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{4}|\d{1,2}\s+(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateToken();
}
