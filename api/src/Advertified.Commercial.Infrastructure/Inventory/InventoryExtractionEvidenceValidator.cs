using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryExtractionEvidenceValidator
{
    private const decimal OverallOcrThreshold = 0.90m;
    private const decimal CriticalOcrThreshold = 0.95m;
    private static readonly HashSet<string> CriticalFields = new(StringComparer.Ordinal)
    {
        "product_code", "name", "channel", "product_type", "geography",
        "latitude", "longitude", "rate", "rate_minor", "currency", "rate_type",
        "vat_treatment", "rate_valid_from", "rate_valid_to", "availability",
    };

    internal static IReadOnlyList<InventoryValidationIssueView> Validate(
        IReadOnlyList<InventoryFieldEvidenceView> evidence)
    {
        var issues = new List<InventoryValidationIssueView>();
        if (evidence.Any(item => string.IsNullOrWhiteSpace(item.SourceLocator)))
        {
            issues.Add(Block(
                "evidence",
                MasterDataCodes.ValidationIssueTypes.EvidencePointerRequired,
                "Every accepted field requires an exact source evidence pointer."));
        }
        var ocr = evidence.Where(item =>
            item.ExtractionMethod == MasterDataCodes.InventoryExtractionMethods.Ocr).ToArray();
        if (ocr.Any(item => !item.ExtractionConfidence.HasValue ||
                item.ExtractionConfidence < OverallOcrThreshold) ||
            ocr.Any(item => CriticalFields.Contains(item.FieldName) &&
                (!item.ExtractionConfidence.HasValue ||
                    item.ExtractionConfidence < CriticalOcrThreshold)))
        {
            issues.Add(Block(
                "extractionConfidence",
                MasterDataCodes.ValidationIssueTypes.ExtractionConfidenceLow,
                "OCR confidence is below the review-ready acceptance threshold."));
        }
        return issues;
    }

    private static InventoryValidationIssueView Block(
        string field,
        string code,
        string message) => new(field, code, message, true);
}
