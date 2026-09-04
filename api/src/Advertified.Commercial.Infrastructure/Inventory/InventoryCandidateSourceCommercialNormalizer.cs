using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static string? SourceChannel(
        Dictionary<string, string> values,
        List<InventoryFieldEvidenceView> evidence,
        string locator,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var supplied = Code(values, "channel");
        if (supplied is not null)
            return supplied;

        var name = Text(values, "name");
        if (!IsKnownDigitalStreamingPlatform(name))
            return null;

        var source = evidence.LastOrDefault(item =>
            item.FieldName == "name");
        evidence.Add(Evidence(
            "channel",
            source?.RawValue ?? name,
            MasterDataCodes.Channels.Digital,
            MasterDataCodes.InventoryTransformationTypes
                .DerivedFromSourceContext,
            source?.SourceLocator ?? locator,
            sourceHash,
            capturedAtUtc,
            MasterDataCodes.InventoryExtractionMethods.PolicyDefault,
            source?.ExtractionConfidence,
            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy));
        return MasterDataCodes.Channels.Digital;
    }

    private static void ApplyKnownBrandCasing(
        Dictionary<string, string> values)
    {
        if (!values.TryGetValue("name", out var name) ||
            !name.StartsWith(
                "dstv",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        values["name"] = "DStv" + name[4..];
    }

    private static bool IsKnownDigitalStreamingPlatform(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var normalized = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized.StartsWith(
                   "dstvstream",
                   StringComparison.Ordinal) ||
               normalized.StartsWith(
                   "youtube",
                   StringComparison.Ordinal);
    }
}
