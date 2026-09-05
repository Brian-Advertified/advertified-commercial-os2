using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryAcceptanceCandidateChecks
{
    internal const string NoCoordinatesCondition = "non-ooh-without-supplied-coordinates/1";
    private static readonly string[] RequiredMeanings =
        ["product_code", "name", "channel", "product_type", "geography"];
    private static readonly string[] PricingMeanings = ["currency", "rate_type"];

    internal static bool RequiredMapping(string? meaning) => meaning is not null &&
        (RequiredMeanings.Contains(meaning, StringComparer.Ordinal) ||
         PricingMeanings.Contains(meaning, StringComparer.Ordinal) || meaning is "rate" or "rate_minor");

    internal static IReadOnlyList<InventoryAcceptanceCheckEvidence> Evaluate(
        PreparedInventoryCandidate candidate, InventoryExtractedRow? row, string sourceHash,
        IReadOnlyList<InventoryValidationIssueView> executedValidation)
    {
        var scope = candidate.SourceLocator;
        var fields = row?.DiscoveredFields ?? [];
        var required = RequiredMeanings.All(meaning => Bound(meaning, fields, candidate));
        if (!InventoryPendingSupplierValidationPolicy.IsPendingSupplier(candidate.Values))
            required &= PricingMeanings.All(meaning => Bound(meaning, fields, candidate)) &&
                (Bound("rate", fields, candidate) || Bound("rate_minor", fields, candidate));
        required &= Bound("availability", fields, candidate) || PolicyAvailability(candidate);
        var ambiguous = candidate.Values.Extension?.ContainsKey(
            InventoryDiscoveredCandidateNormalizer.UnresolvedMarker) == true ||
            candidate.Values.Extension?.ContainsKey("rateambiguity") == true;
        return
        [
            Check(InventoryAcceptanceCheck.RequiredFieldBindings, required, scope,
                required ? "Applicable required commercial fields have source bindings or an explicit existing policy basis."
                    : "One or more required commercial fields lack an evidenced mapping or permitted policy basis."),
            Check(InventoryAcceptanceCheck.CommercialValidation,
                !executedValidation.Any(issue => issue.IsBlocking), scope,
                "Existing commercial, governed value, unit, date, relationship and extraction-evidence validators were executed."),
            Check(InventoryAcceptanceCheck.MaterialAmbiguity, !ambiguous, scope,
                ambiguous ? "Unresolved source meaning or contradictory values affect this candidate."
                    : "No material ambiguity remains in the projected candidate evidence."),
            Check(InventoryAcceptanceCheck.RawEvidence, RawEvidence(candidate, row, sourceHash), scope,
                "Projected original values, source hashes and locations must match retained field evidence."),
            Coordinates(candidate),
        ];
    }

    private static bool Bound(string meaning, IReadOnlyList<InventoryDiscoveredField> fields,
        PreparedInventoryCandidate candidate) => fields.Any(field =>
            field.CanonicalMeaning == meaning && !string.IsNullOrWhiteSpace(field.RawValue) &&
            !string.IsNullOrWhiteSpace(field.Interpretation) && field.Warnings.Count == 0 &&
            candidate.Evidence.Any(evidence => evidence.FieldName == meaning &&
                evidence.SourceLocator == field.SourceLocator && evidence.RawValue == field.RawValue &&
                !string.IsNullOrWhiteSpace(evidence.NormalizedValue)));

    private static bool PolicyAvailability(PreparedInventoryCandidate candidate) =>
        candidate.Values.Availability == MasterDataCodes.AvailabilityStatuses.PlanningAvailable &&
        candidate.Evidence.Any(field => field.FieldName == "availability" &&
            field.RawValue is null && field.NormalizedValue == candidate.Values.Availability &&
            field.SourceLocator == "policy:inventory-availability-default-v1" &&
            field.ExtractionMethod == MasterDataCodes.InventoryExtractionMethods.PolicyDefault &&
            field.EvidenceBasis == MasterDataCodes.InventoryEvidenceBases.DerivedPolicy);

    private static bool RawEvidence(PreparedInventoryCandidate candidate, InventoryExtractedRow? row, string hash)
    {
        if (row?.DiscoveredFields is not { Count: > 0 } fields || row.Locator != candidate.SourceLocator)
            return false;
        return candidate.Evidence.All(item => item.SourceHash == hash &&
                !string.IsNullOrWhiteSpace(item.SourceLocator)) &&
            fields.All(field => candidate.Evidence.Any(item => item.SourceLocator == field.SourceLocator &&
                item.RawValue == field.RawValue));
    }

    private static InventoryAcceptanceCheckEvidence Coordinates(PreparedInventoryCandidate candidate)
    {
        var values = candidate.Values;
        var applicable = values.Channel is MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh ||
            values.Latitude.HasValue || values.Longitude.HasValue || candidate.Evidence.Any(field =>
                field.FieldName is "latitude" or "longitude" && !string.IsNullOrWhiteSpace(field.RawValue));
        return applicable
            ? Check(InventoryAcceptanceCheck.CoordinateApplicability,
                values.Latitude is >= -90 and <= 90 && values.Longitude is >= -180 and <= 180,
                candidate.SourceLocator, "Supplied or OOH-required coordinates must be paired and within WGS84 ranges.")
            : new(InventoryAcceptanceCheck.CoordinateApplicability, InventoryAcceptanceCheckResult.NotApplicable,
                candidate.SourceLocator, "This non-OOH candidate supplies no coordinates; coordinates are optional.",
                NoCoordinatesCondition);
    }

    private static InventoryAcceptanceCheckEvidence Check(InventoryAcceptanceCheck check, bool passed,
        string scope, string reason) => new(check,
            passed ? InventoryAcceptanceCheckResult.Passed : InventoryAcceptanceCheckResult.Failed, scope, reason);
}
