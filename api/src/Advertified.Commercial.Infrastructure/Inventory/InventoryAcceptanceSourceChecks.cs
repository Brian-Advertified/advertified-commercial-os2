using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryAcceptanceSourceChecks
{
    internal static IReadOnlyList<InventoryAcceptanceCheckEvidence> Evaluate(
        InventoryExtractionResult extraction,
        string expectedHash,
        long sourceVersion,
        InventoryCodeSets codes)
    {
        _ = codes;
        var identity =
            sourceVersion > 0 &&
            extraction.SourceHash == expectedHash &&
            extraction.ProviderOutputHash ==
                InventoryExtractionContract.Hash(
                    extraction.ProviderJson) &&
            extraction.CanonicalOutputHash ==
                InventoryExtractionContract.Hash(
                    extraction.CanonicalJson);
        var sourceElements =
            extraction.Document.SourceElements ?? [];
        var locators = sourceElements
            .Select(element => element.Locator)
            .ToHashSet(StringComparer.Ordinal);
        var referenced = extraction.Rows
            .SelectMany(RowLocators)
            .ToArray();
        var bindingsValid =
            sourceElements.Count > 0 &&
            referenced.Length > 0 &&
            referenced.All(locators.Contains);
        var accounting =
            extraction.Document.SourceAccounting?.Summary;
        var accountingReport =
            extraction.Document.SourceAccounting;
        var accounted =
            accounting is not null &&
            accounting.UnaccountedCommercialElements == 0 &&
            accountingReport!.ExceptionGroups.Count == 0;

        return
        [
            Check(
                InventoryAcceptanceCheck.SourceIdentity,
                identity,
                identity
                    ? "Source version and retained extraction hashes match the active attempt."
                    : "Source version or retained extraction hash does not match the active attempt."),
            Check(
                InventoryAcceptanceCheck.InterpretationBinding,
                bindingsValid,
                bindingsValid
                    ? "Every projected field is bound to a retained Python source element."
                    : "One or more projected fields lack a retained source-element binding."),
            Check(
                InventoryAcceptanceCheck.StructuralApplication,
                bindingsValid,
                bindingsValid
                    ? "The versioned Python projection returned exact structural lineage."
                    : "The Python projection lineage is incomplete."),
            new InventoryAcceptanceCheckEvidence(
                InventoryAcceptanceCheck.SourceContentAccounting,
                accounting is null
                    ? InventoryAcceptanceCheckResult.NotEvaluated
                    : accounted
                        ? InventoryAcceptanceCheckResult.Passed
                        : InventoryAcceptanceCheckResult.Failed,
                "document",
                accounting is null
                    ? "Source accounting has not been attached."
                    : accounted
                        ? "All detected commercial-looking source elements are resolved with terminal dispositions."
                        : "Commercial-looking source elements remain unresolved or unaccounted."),
        ];
    }

    private static IEnumerable<string> RowLocators(
        InventoryExtractedRow row) =>
        (row.FieldLocators?.Values ?? [])
        .Concat((row.RateVariants ?? [])
            .Select(rate => rate.SourceLocator));

    private static InventoryAcceptanceCheckEvidence Check(
        InventoryAcceptanceCheck check,
        bool passed,
        string reason) => new(
            check,
            passed
                ? InventoryAcceptanceCheckResult.Passed
                : InventoryAcceptanceCheckResult.Failed,
            "document",
            reason);
}
