using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Infrastructure.MasterData;
namespace Advertified.Commercial.Infrastructure.Brief;

internal sealed partial class SuppliedBriefBudgetParser
{
    private readonly string defaultCurrency;
    private readonly IReadOnlyDictionary<string, CurrencyRule> currenciesByMarker;
    private readonly Regex currencyBeforeAmount;
    private readonly Regex currencyAfterAmount;

    public SuppliedBriefBudgetParser(SuppliedBriefAgentPolicy policy)
    {
        defaultCurrency = policy.DefaultCurrency;
        var rules = policy.ActiveCurrencies.Select(currency => new CurrencyRule(
            currency.Code, currency.MinorUnitDigits, currency.BriefMarkers)).ToArray();
        currenciesByMarker = rules
            .SelectMany(rule => rule.Markers.Select(marker => (Marker: Normalize(marker), Rule: rule)))
            .ToDictionary(item => item.Marker, item => item.Rule, StringComparer.Ordinal);
        if (!currenciesByMarker.Values.Any(rule => rule.Code == defaultCurrency))
        {
            throw new InvalidOperationException(
                "The default Brief currency has no active deterministic parsing rule.");
        }
        var markerPattern = string.Join('|', rules
            .SelectMany(rule => rule.Markers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(marker => marker.Length)
            .Select(Regex.Escape));
        currencyBeforeAmount = CurrencyExpression(markerPattern, currencyFirst: true);
        currencyAfterAmount = CurrencyExpression(markerPattern, currencyFirst: false);
    }

    public ParsedSuppliedBriefBudget? Parse(string? value, bool allowBareDefault = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var matches = ExplicitMatches(value).ToArray();
        if (matches.Length > 0)
        {
            var parsed = new List<ParsedSuppliedBriefBudget>();
            foreach (var match in matches)
            {
                var candidate = ParseExplicit(match);
                if (candidate is null) return null;
                parsed.Add(candidate);
            }
            return parsed.Distinct().Count() == 1 ? parsed[0] : null;
        }
        var defaultMatch = LabelledDefaultBudget().Match(value);
        if (!defaultMatch.Success && allowBareDefault)
        {
            defaultMatch = BareDefaultBudget().Match(value);
        }
        return defaultMatch.Success
            ? ParseAmount(defaultMatch, RuleForCode(defaultCurrency))
            : null;
    }

    public string? ExtractEvidence(string content)
    {
        var explicitMatch = ExplicitMatches(content).FirstOrDefault();
        if (explicitMatch is not null) return explicitMatch.Value.Trim();
        var defaultMatch = LabelledDefaultBudget().Match(content);
        return defaultMatch.Success ? defaultMatch.Value.Trim() : null;
    }

    private IEnumerable<Match> ExplicitMatches(string value) =>
        currencyBeforeAmount.Matches(value).Cast<Match>()
            .Concat(currencyAfterAmount.Matches(value).Cast<Match>())
            .OrderBy(match => match.Index);

    private static Regex CurrencyExpression(string markers, bool currencyFirst)
    {
        const string amount = @"(?<amount>(?:\d{1,3}(?:[ ,]\d{3})+|\d+)(?:\.\d+)?)\s*(?<unit>k|m|thousand|million)?";
        var currency = $@"(?<currency>{markers})";
        var expression = currencyFirst
            ? $@"(?<![\p{{L}}\p{{N}}.,]){currency}\s*{amount}(?![\p{{L}}\p{{N}}.,])"
            : $@"(?<![\p{{L}}\p{{N}}.,]){amount}\s*{currency}(?![\p{{L}}\p{{N}}.,])";
        return new Regex(
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }

    private ParsedSuppliedBriefBudget? ParseExplicit(Match match)
    {
        var marker = Normalize(match.Groups["currency"].Value);
        return currenciesByMarker.TryGetValue(marker, out var rule)
            ? ParseAmount(match, rule)
            : null;
    }

    private static ParsedSuppliedBriefBudget? ParseAmount(Match match, CurrencyRule rule)
    {
        var raw = string.Concat(match.Groups["amount"].Value.Where(character =>
            !char.IsWhiteSpace(character))).Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount)) return null;
        var multiplier = match.Groups["unit"].Value.Trim().ToLowerInvariant() switch
        {
            "k" or "thousand" => 1_000m,
            "m" or "million" => 1_000_000m,
            "" => 1m,
            _ => 0m,
        };
        if (multiplier == 0m) return null;
        try
        {
            var minor = CurrencyMetadata.MajorToMinor(
                amount * multiplier, rule.IsoFractionDigits);
            return minor is null
                ? null
                : new ParsedSuppliedBriefBudget(minor.Value, rule.Code);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private CurrencyRule RuleForCode(string code) => currenciesByMarker.Values
        .First(rule => string.Equals(rule.Code, code, StringComparison.Ordinal));

    private static string Normalize(string marker) =>
        string.Concat(marker.Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

    [GeneratedRegex(@"(?im)^\s*(?:media\s+)?budget\s*(?::|=|-|\bis\b|\bof\b)\s*(?<amount>(?:\d{1,3}(?:[ ,]\d{3})+|\d+)(?:\.\d+)?)\s*(?<unit>k|m|thousand|million)?\s*(?:[.;]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabelledDefaultBudget();

    [GeneratedRegex(@"^\s*(?<amount>(?:\d{1,3}(?:[ ,]\d{3})+|\d+)(?:\.\d+)?)\s*(?<unit>k|m|thousand|million)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareDefaultBudget();

    private sealed record CurrencyRule(string Code, int IsoFractionDigits, string[] Markers);
}

internal sealed record ParsedSuppliedBriefBudget(long AmountMinor, string Currency);
