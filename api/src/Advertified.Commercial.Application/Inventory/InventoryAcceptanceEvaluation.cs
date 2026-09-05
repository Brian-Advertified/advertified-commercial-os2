using System.Text.Json.Serialization;

namespace Advertified.Commercial.Application.Inventory;

// Versioned acceptance evidence protocol. These are check outcomes, not lifecycle states.
[JsonConverter(typeof(JsonStringEnumConverter<InventoryAcceptanceCheckResult>))]
public enum InventoryAcceptanceCheckResult { Passed, Failed, NotEvaluated, NotApplicable }

[JsonConverter(typeof(JsonStringEnumConverter<InventoryAcceptanceCheck>))]
public enum InventoryAcceptanceCheck
{
    SourceIdentity, InterpretationBinding, StructuralApplication, SourceContentAccounting,
    RequiredFieldBindings, CommercialValidation, MaterialAmbiguity, RawEvidence,
    CoordinateApplicability,
}

public sealed record InventoryAcceptanceCheckEvidence(
    InventoryAcceptanceCheck Check, InventoryAcceptanceCheckResult Result,
    string Scope, string Reason, string? ApplicabilityCondition = null);

public sealed record InventoryAcceptanceEvaluation(
    string PolicyVersion, string SourceHash, long SourceFileVersion,
    string ExtractionRevision, string MappingRevision, string CandidateRevision,
    InventorySchemaProvenance? Provenance, DateTimeOffset EvaluatedAtUtc,
    string Outcome, IReadOnlyList<InventoryAcceptanceCheckEvidence> Checks);
