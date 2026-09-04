using System.Security.Cryptography;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryExtractionCompletionPolicy
{
    internal static void VerifySource(byte[] content, string expectedHash)
    {
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual), Convert.FromHexString(expectedHash)))
        {
            throw new InventoryProtectionUnavailableException();
        }
    }

    internal static void VerifyResult(
        InventoryExtractionResult extraction,
        string expectedSourceHash)
    {
        InventoryExtractionContract.Replay(
            extraction.CanonicalJson, InventoryExtractionOptions.CurrentSchemaVersion);
        var valid = string.Equals(
                extraction.SourceHash, expectedSourceHash, StringComparison.Ordinal) &&
            string.Equals(extraction.ProviderOutputHash,
                InventoryExtractionContract.Hash(extraction.ProviderJson),
                StringComparison.Ordinal) &&
            string.Equals(extraction.CanonicalOutputHash,
                InventoryExtractionContract.Hash(extraction.CanonicalJson),
                StringComparison.Ordinal) &&
            string.Equals(extraction.CanonicalJson,
                InventoryExtractionContract.Serialize(extraction.Document),
                StringComparison.Ordinal) &&
            extraction.SchemaVersion == InventoryExtractionOptions.CurrentSchemaVersion &&
            !string.IsNullOrWhiteSpace(extraction.AdapterVersion);
        if (!valid)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    internal static PreparedInventoryCandidate PrepareCandidate(
        ExtractedInventoryCandidate extracted,
        string selectedSupplier,
        InventoryCodeSets codes)
    {
        var validation = InventoryCandidateValidator.Validate(extracted.Values, codes)
            .Concat(InventoryExtractionEvidenceValidator.Validate(extracted.Evidence))
            .Concat(ValidateSupplierIdentity(extracted.SupplierName, selectedSupplier))
            .ToArray();
        return new PreparedInventoryCandidate(
            Guid.NewGuid(), extracted.RowNumber, extracted.Values, validation,
            extracted.Locator, extracted.Evidence, Guid.NewGuid());
    }

    private static IReadOnlyList<InventoryValidationIssueView> ValidateSupplierIdentity(
        string? extractedSupplier,
        string selectedSupplier)
    {
        if (string.IsNullOrWhiteSpace(extractedSupplier) ||
            IsUnspecifiedSupplier(selectedSupplier) ||
            string.Equals(NormalizeSupplier(extractedSupplier),
                NormalizeSupplier(selectedSupplier), StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
        return [new InventoryValidationIssueView(
            "supplierName",
            MasterDataCodes.ValidationIssueTypes.SupplierIdentityMismatch,
            "The extracted supplier differs from the supplier selected for this import.",
            false)];
    }

    private static bool IsUnspecifiedSupplier(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(
            NormalizeSupplier(value),
            "Not supplied",
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSupplier(string value) =>
        string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
