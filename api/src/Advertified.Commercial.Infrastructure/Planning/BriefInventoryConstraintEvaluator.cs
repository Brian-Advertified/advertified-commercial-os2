using System.Text.RegularExpressions;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static partial class BriefInventoryConstraintEvaluator
{
    internal static EligibilityResult? Evaluate(
        PlanningInventoryRow inventory,
        IReadOnlyList<string> constraints)
    {
        if (constraints.Count == 0) return null;
        var requirements = Normalize(string.Join(" ", constraints));
        var inventoryDescription = Normalize($"{inventory.Name} {inventory.ProductType}");

        if (RequiresDigitalOnly(requirements) &&
            inventory.Channel != MasterDataCodes.Channels.Dooh)
        {
            return Rejected(
                "The approved Brief permits digital out-of-home inventory only.");
        }
        if (RequiresLargeFormat(requirements) &&
            !inventoryDescription.Contains("large format", StringComparison.Ordinal))
        {
            return Rejected(
                "The inventory has no canonical large-format evidence.");
        }
        if (ExcludesThreeBySix(requirements) && ThreeBySix().IsMatch(inventoryDescription))
        {
            return Rejected(
                "The approved Brief excludes 3 × 6 inventory.");
        }
        return null;
    }

    private static bool RequiresDigitalOnly(string value) =>
        value.Contains("only digital", StringComparison.Ordinal) ||
        value.Contains("digital only", StringComparison.Ordinal);

    private static bool RequiresLargeFormat(string value) =>
        value.Contains("large format", StringComparison.Ordinal);

    private static bool ExcludesThreeBySix(string value) =>
        value.Contains("do not use 3 x 6", StringComparison.Ordinal) ||
        value.Contains("exclude 3 x 6", StringComparison.Ordinal) ||
        value.Contains("no 3 x 6", StringComparison.Ordinal);

    private static string Normalize(string value) =>
        Whitespace().Replace(value.Replace('×', 'x').Replace('-', ' ').ToLowerInvariant(), " ").Trim();

    private static EligibilityResult Rejected(string detail) =>
        new(false, MasterDataCodes.RejectionReasons.IneligibleFormat, detail, null);

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\b3\s*x\s*6\b", RegexOptions.CultureInvariant)]
    private static partial Regex ThreeBySix();
}
