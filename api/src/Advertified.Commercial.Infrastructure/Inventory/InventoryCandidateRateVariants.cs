using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static InventoryCandidateValues ApplyRateVariants(
        InventoryCandidateValues values,
        InventoryExtractedRow row,
        List<InventoryFieldEvidenceView> evidence,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        if (row.RateVariants is not { Count: > 0 }) return values;
        var normalized = row.RateVariants.Select((variant, index) =>
                NormalizeRateVariant(variant, index, values.Currency, row,
                    evidence, sourceHash, capturedAtUtc))
            .ToArray();
        var primary = normalized[0];
        return values with
        {
            RateType = primary.RateType ?? values.RateType,
            Currency = primary.Currency ?? values.Currency,
            RateAmountMinor = primary.AmountMinor ?? values.RateAmountMinor,
            RateVariants = normalized,
            ProductVariants = ProductVariants(normalized),
        };
    }

    private static InventoryRateVariantValues NormalizeRateVariant(
        InventoryExtractedRateVariant variant,
        int index,
        string? inheritedCurrency,
        InventoryExtractedRow row,
        List<InventoryFieldEvidenceView> evidence,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var currency = NormalizeCurrency(variant.Currency ?? inheritedCurrency);
        var amount = ParseRate(variant.RawValue, currency);
        var prefix = "rateVariant[" + index.ToString("D4", CultureInfo.InvariantCulture) + "]";
        evidence.Add(Evidence(prefix + ".amount", variant.RawValue,
            amount?.ToString(CultureInfo.InvariantCulture),
            MasterDataCodes.InventoryTransformationTypes.MajorToMinor,
            variant.SourceLocator, sourceHash, capturedAtUtc,
            row.ExtractionMethod ?? MasterDataCodes.InventoryExtractionMethods.Tabular,
            row.Confidence));
        evidence.Add(Evidence(prefix + ".headerHierarchy",
            variant.HeaderHierarchy, variant.HeaderHierarchy,
            MasterDataCodes.InventoryTransformationTypes.Trim,
            variant.HeaderLocators.Count == 0
                ? variant.SourceLocator
                : variant.HeaderLocators[^1],
            sourceHash, capturedAtUtc,
            row.ExtractionMethod ?? MasterDataCodes.InventoryExtractionMethods.Tabular,
            row.Confidence));
        if (currency is not null)
            evidence.Add(Evidence(prefix + ".currency", variant.Currency,
                currency, MasterDataCodes.InventoryTransformationTypes.UppercaseCode,
                variant.SourceLocator, sourceHash, capturedAtUtc,
                row.ExtractionMethod ?? MasterDataCodes.InventoryExtractionMethods.Tabular,
                row.Confidence));
        AddDimensionEvidence(variant, prefix, row, evidence,
            sourceHash, capturedAtUtc);
        return new InventoryRateVariantValues(variant.RateType, amount, currency,
            variant.BuyingUnit, variant.ValidFrom, variant.ValidTo, variant.Geography,
            variant.Daypart, variant.Days, variant.DurationSeconds,
            variant.SourceLocator, variant.RawValue, variant.HeaderHierarchy,
            variant.HeaderLocators, variant.Dimensions);
    }

    private static void AddDimensionEvidence(
        InventoryExtractedRateVariant variant,
        string prefix,
        InventoryExtractedRow row,
        List<InventoryFieldEvidenceView> evidence,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var headerLocator = variant.HeaderLocators.Count == 0
            ? variant.SourceLocator
            : variant.HeaderLocators[^1];
        AddDerived(evidence, prefix + ".rateType", variant.RateType,
            headerLocator, row, sourceHash, capturedAtUtc);
        AddDerived(evidence, prefix + ".buyingUnit", variant.BuyingUnit,
            headerLocator, row, sourceHash, capturedAtUtc);
        AddDerived(evidence, prefix + ".geography", variant.Geography,
            headerLocator, row, sourceHash, capturedAtUtc);
        AddDerived(evidence, prefix + ".daypart", variant.Daypart,
            headerLocator, row, sourceHash, capturedAtUtc);
        AddDerived(evidence, prefix + ".days", variant.Days,
            headerLocator, row, sourceHash, capturedAtUtc);
        AddDerived(evidence, prefix + ".durationSeconds",
            variant.DurationSeconds?.ToString(CultureInfo.InvariantCulture),
            headerLocator, row, sourceHash, capturedAtUtc);
        foreach (var dimension in (variant.Dimensions ??
                     new Dictionary<string, string>()).OrderBy(item => item.Key,
                     StringComparer.Ordinal))
        {
            var index = int.TryParse(dimension.Key.AsSpan("header_".Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed - 1 : -1;
            var locator = index >= 0 && index < variant.HeaderLocators.Count
                ? variant.HeaderLocators[index]
                : headerLocator;
            AddDerived(evidence, prefix + ".dimension." + dimension.Key,
                dimension.Value, locator, row, sourceHash, capturedAtUtc);
        }
    }

    private static void AddDerived(
        List<InventoryFieldEvidenceView> evidence,
        string field,
        string? value,
        string locator,
        InventoryExtractedRow row,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        evidence.Add(Evidence(field, value, value,
            MasterDataCodes.InventoryTransformationTypes.DerivedFromSourceContext,
            locator, sourceHash, capturedAtUtc,
            row.ExtractionMethod ?? MasterDataCodes.InventoryExtractionMethods.Tabular,
            row.Confidence));
    }

    private static long? ParseRate(string raw, string? currency)
    {
        if (InventoryMoneyParser.IsAmbiguousTruncatedRate(raw) ||
            !InventoryMoneyParser.TryParse(raw, out var amount, out var parsed))
            return null;
        return MajorRateToMinor(amount, currency ??
            (parsed.Length > 0 ? parsed : null));
    }

    private static string? NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var code = value.Trim().ToUpperInvariant();
        return code == "R" ? MasterDataCodes.Currencies.Zar : code;
    }

    private static InventoryProductVariantValues[]? ProductVariants(
        IReadOnlyList<InventoryRateVariantValues> rates)
    {
        var grouped = rates.Where(rate => rate.Geography is not null)
            .GroupBy(rate => rate.Geography!, StringComparer.Ordinal)
            .Select(group => new InventoryProductVariantValues(
                "geography:" + group.Key,
                group.Key,
                group.Key,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["geography"] = group.Key,
                },
                group.Select(rate => rate.SourceLocator).ToArray()))
            .ToArray();
        return grouped.Length == 0 ? null : grouped;
    }
}
