using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Implements ADVERTIFIED 11.23. No model confidence score grants acceptance.
internal static class InventoryAcceptancePolicy
{
    internal const string Version = "inventory-acceptance/1.0";
    internal const string EvidenceKey = "acceptanceevaluation";

    internal static PreparedInventoryCandidate[] Apply(
        InventoryExtractionResult extraction, string expectedSourceHash, long sourceFileVersion,
        InventoryCodeSets codes, IReadOnlyList<PreparedInventoryCandidate> candidates,
        DateTimeOffset now)
    {
        var documentChecks = InventoryAcceptanceSourceChecks.Evaluate(
            extraction, expectedSourceHash, sourceFileVersion, codes);
        var rows = extraction.Rows.ToDictionary(row => row.Number);
        return candidates.Select(candidate => candidate.HasDiscoveredSchema
            ? Evaluate(candidate, rows.GetValueOrDefault(candidate.RowNumber), extraction,
                sourceFileVersion, codes, documentChecks, now)
            : candidate).ToArray();
    }

    private static PreparedInventoryCandidate Evaluate(PreparedInventoryCandidate candidate,
        InventoryExtractedRow? row, InventoryExtractionResult extraction, long sourceFileVersion,
        InventoryCodeSets codes, IReadOnlyList<InventoryAcceptanceCheckEvidence> documentChecks,
        DateTimeOffset now)
    {
        // Execute validators now: a historical empty issue list is not proof of execution.
        var issues = InventoryPendingSupplierValidationPolicy.Apply(candidate.Values,
            InventoryCandidateValidator.Validate(candidate.Values, codes)
                .Concat(InventoryExtractionEvidenceValidator.Validate(candidate.Evidence)));
        var checks = documentChecks.Concat(InventoryAcceptanceCandidateChecks.Evaluate(
            candidate, row, extraction.SourceHash, issues)).ToArray();
        var outcome = Outcome(checks);
        var schema = extraction.Document.DiscoveredSchema;
        var evaluation = new InventoryAcceptanceEvaluation(Version, extraction.SourceHash,
            sourceFileVersion, extraction.CanonicalOutputHash,
            schema is null ? string.Empty : MappingRevision(schema),
            CandidateRevision(candidate.Values), schema?.Provenance, now, outcome, checks);
        var extension = new Dictionary<string, string>(candidate.Values.Extension ??
            new Dictionary<string, string>(), StringComparer.Ordinal);
        extension.Remove(InventoryCandidateReviewPolicy.AutoCertifiedMarker);
        extension[EvidenceKey] = JsonSerializer.Serialize(evaluation, InventoryRowMapper.StoredJson);
        var policyIssues = checks.Where(check => !Passes(check)).Select(check =>
            new InventoryValidationIssueView("acceptance." + check.Check,
                MasterDataCodes.ValidationIssueTypes.CommercialTermsInvalid, check.Reason, true));
        return candidate with
        {
            Values = candidate.Values with { Extension = extension },
            Validation = issues.Concat(policyIssues).ToArray(),
        };
    }

    internal static string Outcome(IReadOnlyList<InventoryAcceptanceCheckEvidence> checks)
    {
        if (checks.Any(check => check.Result == InventoryAcceptanceCheckResult.Failed))
            return MasterDataCodes.LifecycleStatuses.ReviewRequired;
        return Complete(checks) ? MasterDataCodes.LifecycleStatuses.Approved
            : MasterDataCodes.LifecycleStatuses.Pending;
    }

    internal static bool Complete(IReadOnlyList<InventoryAcceptanceCheckEvidence> checks) =>
        checks.Count == Enum.GetValues<InventoryAcceptanceCheck>().Length &&
        checks.All(check => Enum.IsDefined(check.Check)) &&
        checks.Select(check => check.Check).Distinct().Count() == checks.Count &&
        checks.All(Passes);

    private static bool Passes(InventoryAcceptanceCheckEvidence check) =>
        !string.IsNullOrWhiteSpace(check.Reason) &&
        (check.Result == InventoryAcceptanceCheckResult.Passed ||
         check.Result == InventoryAcceptanceCheckResult.NotApplicable &&
         check.Check == InventoryAcceptanceCheck.CoordinateApplicability &&
         check.ApplicabilityCondition == InventoryAcceptanceCandidateChecks.NoCoordinatesCondition);

    internal static InventoryAcceptanceEvaluation? Read(InventoryCandidateValues values)
    {
        if (values.Extension is null || !values.Extension.TryGetValue(EvidenceKey, out var json) ||
            string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<InventoryAcceptanceEvaluation>(json, InventoryRowMapper.StoredJson); }
        catch (JsonException) { return null; }
    }

    internal static bool CanAccept(InventoryCandidateValues values)
    {
        var evaluation = Read(values);
        return evaluation is not null && evaluation.PolicyVersion == Version &&
            evaluation.SourceFileVersion > 0 && !string.IsNullOrWhiteSpace(evaluation.SourceHash) &&
            !string.IsNullOrWhiteSpace(evaluation.ExtractionRevision) &&
            !string.IsNullOrWhiteSpace(evaluation.MappingRevision) && evaluation.Provenance is not null &&
            evaluation.CandidateRevision == CandidateRevision(values) &&
            evaluation.Outcome == MasterDataCodes.LifecycleStatuses.Approved && Complete(evaluation.Checks);
    }

    internal static string CandidateRevision(InventoryCandidateValues values)
    {
        var extension = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in values.Extension ?? new Dictionary<string, string>())
            if (pair.Key != EvidenceKey && pair.Key != InventoryCandidateReviewPolicy.AutoCertifiedMarker)
                extension[pair.Key] = pair.Value;
        return InventoryExtractionContract.Hash(JsonSerializer.Serialize(values with
        {
            Extension = extension.Count == 0 ? null : extension,
        }, InventoryRowMapper.StoredJson));
    }

    internal static string MappingRevision(DiscoveredInventorySchema schema) =>
        InventoryExtractionContract.Hash(JsonSerializer.Serialize(schema, InventoryRowMapper.StoredJson));
}
