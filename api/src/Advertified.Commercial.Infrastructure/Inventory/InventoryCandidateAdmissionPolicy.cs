using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryCandidateAdmissionPolicy
{
    private static readonly HashSet<string> IdentityFields =
    [
        "name",
        "productCode",
        "product_code",
        "platform",
        "siteCode",
        "site_code",
        "siteNumber",
        "site_number",
    ];

    internal static PreparedInventoryCandidate[] Prepare(
        IReadOnlyList<InventoryExtractedRow> rows,
        string sourceHash,
        string selectedSupplier,
        InventoryCodeSets codes,
        DateTimeOffset capturedAtUtc,
        string? sourceFileName = null)
    {
        var result = new List<PreparedInventoryCandidate>();
        foreach (var row in rows)
        {
            var candidate = InventoryCandidateNormalizer.Normalize(row, sourceHash, capturedAtUtc);
            if (candidate.HasDiscoveredSchema)
            {
                // Record boundaries come from the validated schema. Missing meanings
                // are review exceptions, not permission to discard the source record.
                if (!candidate.Evidence.Any(field => HasText(field.RawValue))) continue;
            }
            else if (!IsSellableCandidate(candidate)) continue;
            var prepared = InventoryExtractionCompletionPolicy.PrepareCandidate(candidate, selectedSupplier, codes);
            result.Add(prepared);
        }
        return result.Where(candidate =>
                candidate.HasDiscoveredSchema || IsAdmitted(candidate))
            .ToArray();
    }

    internal static bool IsSellableCandidate(
        ExtractedInventoryCandidate candidate)
    {
        var values = candidate.Values;
        if (HasText(values.ProductCode) ||
            HasText(values.Package?.PackageCode) ||
            HasText(values.Package?.PackageName))
        {
            return true;
        }
        if (!HasText(values.Name))
            return false;

        var hasRawRate = candidate.Evidence.Any(item =>
            item.FieldName == "rate" && HasText(item.RawValue));
        var hasSourceAvailability = candidate.Evidence.Any(item =>
            item.FieldName == "availability" &&
            item.RawValue is not null);
        return values.RateAmountMinor.HasValue ||
            hasRawRate ||
            values.Deliverable is not null ||
            HasText(values.Geography) ||
            HasText(values.Address) ||
            values.Spatial is not null ||
            hasSourceAvailability;
    }

    internal static bool IsAdmitted(
        PreparedInventoryCandidate candidate)
    {
        var values = candidate.Values;
        if (string.IsNullOrWhiteSpace(values.Name) &&
            string.IsNullOrWhiteSpace(values.ProductCode))
        {
            return false;
        }
        if (!HasSupplierEvidence(candidate.Evidence))
        {
            return false;
        }
        return HasMeaningfulSupplierProductCode(candidate) ||
            HasText(values.Package?.PackageCode) ||
            HasText(values.Package?.PackageName) ||
            !LooksLikeDocumentFurniture(values.Name);
    }

    private static bool HasText(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    private static bool HasSupplierEvidence(
        IReadOnlyList<InventoryFieldEvidenceView> evidence) =>
        evidence.Any(item =>
            item.EvidenceBasis !=
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy &&
            !string.IsNullOrWhiteSpace(item.RawValue) &&
            (IdentityFields.Contains(item.FieldName) ||
             item.FieldName.Equals(
                 "description",
                 StringComparison.OrdinalIgnoreCase) ||
             item.FieldName.Equals(
                 "placement",
                 StringComparison.OrdinalIgnoreCase) ||
             item.FieldName.Equals(
                 "programme",
                 StringComparison.OrdinalIgnoreCase) ||
             item.FieldName.Equals(
                 "daypart",
                 StringComparison.OrdinalIgnoreCase)));

    private static bool HasMeaningfulSupplierProductCode(
        PreparedInventoryCandidate candidate)
    {
        var code = candidate.Values.ProductCode?.Trim();
        if (!HasText(code) || LooksLikeDocumentFurniture(code))
            return false;
        var hasEvidence = candidate.Evidence.Any(item =>
            item.EvidenceBasis !=
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy &&
            !string.IsNullOrWhiteSpace(item.RawValue) &&
            item.FieldName is
                "productCode" or "product_code" or
                "siteCode" or "site_code" or
                "siteNumber" or "site_number");
        if (!hasEvidence)
            return false;
        var values = candidate.Values;
        return code!.Length > 1 ||
            values.RateAmountMinor.HasValue ||
            HasText(values.Name) ||
            values.Deliverable is not null ||
            HasText(values.Geography) ||
            HasText(values.Address) ||
            values.Spatial is not null;
    }

    private static bool LooksLikeDocumentFurniture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        var normalized = string.Join(
            ' ',
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            .Trim()
            .ToUpperInvariant();
        return normalized is
            "RATE" or "RATES" or "RATE CARD" or "NET RATES" or
            "TIME BAND" or "DESCRIPTION" or "FORMAT" or "TYPE" or
            "AREA" or "CITY/PROV." or "CONTACT US" or "THANK YOU" or
            "TERMS AND CONDITIONS" or "NOTES" or "INVESTMENT SUMMARY" or
            "RATE/NO OF WEEKS" or "RATE / NO OF WEEKS" or
            "SIGNATURE" or "DATE" or
            "VAT" or "LESS DISCOUNT" or "DISCOUNT" or "COST PER MONTH" or
            "SAVINGS" or "SAVING" ||
            IsScheduleAllocationHeader(normalized) ||
            normalized.StartsWith("SIGNATURE ", StringComparison.Ordinal) ||
            normalized.StartsWith("VAT ", StringComparison.Ordinal) ||
            normalized.StartsWith("LESS DISCOUNT ", StringComparison.Ordinal) ||
            normalized.Contains(" LESS DISCOUNT ", StringComparison.Ordinal) ||
            normalized.StartsWith("TOTAL ", StringComparison.Ordinal) ||
            normalized.StartsWith("SUBTOTAL", StringComparison.Ordinal) ||
            normalized.StartsWith("SUB TOTAL", StringComparison.Ordinal);
    }

    private static bool IsScheduleAllocationHeader(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 1 && tokens.All(token =>
            token.Length == 1 && token[0] is >= 'A' and <= 'H');
    }

}
