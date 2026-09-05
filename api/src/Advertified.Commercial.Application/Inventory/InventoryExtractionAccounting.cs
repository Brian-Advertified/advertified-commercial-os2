using System.Text.Json.Serialization;

namespace Advertified.Commercial.Application.Inventory;

// Versioned diagnostic protocol carried by the retained extraction artifact.
// These codes describe evidence movement; they are not commercial lifecycle state.
public static class InventoryExtractionTraceCodes
{
    public const string Retained = "RETAINED";
    public const string Mapped = "MAPPED";
    public const string Consolidated = "CONSOLIDATED";
    public const string Rejected = "REJECTED";
    public const string Ambiguous = "AMBIGUOUS";
    public const string Unsupported = "UNSUPPORTED";
    public const string Failed = "FAILED";
    public const string NotEvaluated = "NOT_EVALUATED";

    public const string OriginalSource = "ORIGINAL_SOURCE";
    public const string RawStructure = "RAW_STRUCTURE";
    public const string CommercialBlock = "COMMERCIAL_BLOCK";
    public const string SchemaDiscovery = "SCHEMA_DISCOVERY";
    public const string RecordProjection = "RECORD_PROJECTION";
    public const string Normalization = "NORMALIZATION";
    public const string Admission = "ADMISSION";
    public const string Deduplication = "DEDUPLICATION";
    public const string Persistence = "PERSISTENCE";
    public const string Reconciliation = "RECONCILIATION";

    public const string ProductField = "PRODUCT_FIELD";
    public const string Rate = "RATE";
    public const string PackageOrComponent = "PACKAGE_OR_COMPONENT";
    public const string SharedCondition = "SHARED_CONDITION";
    public const string NonCommercial = "NON_COMMERCIAL";
}

public sealed record InventoryTraceStageResult(
    string State,
    string? Reference = null,
    string? Reason = null);

public sealed record InventorySourceEntryTrace(
    string EntryId,
    string SourceHash,
    string SourceLocator,
    string StructureId,
    string StructureKind,
    int Row,
    int Column,
    string RawValue,
    string? PositionJson,
    string CommercialSignal,
    string? HeaderHierarchy,
    InventoryTraceStageResult OriginalSource,
    InventoryTraceStageResult RawStructure,
    InventoryTraceStageResult CommercialBlock,
    InventoryTraceStageResult SchemaDiscovery,
    InventoryTraceStageResult RecordProjection,
    InventoryTraceStageResult Normalization,
    InventoryTraceStageResult Admission,
    InventoryTraceStageResult Deduplication,
    InventoryTraceStageResult Persistence,
    InventoryTraceStageResult Reconciliation,
    string TerminalDisposition,
    string? FirstFailureStage,
    IReadOnlyList<int> CandidateRows);

public sealed record InventoryDeduplicationDecision(
    string RetainedLocator,
    string ConsolidatedLocator,
    string Reason,
    IReadOnlyList<string> ConsolidatedSourceLocators);

public sealed record InventoryReviewExceptionGroup(
    string GroupKey,
    string Cause,
    string Scope,
    int AffectedEntryCount,
    IReadOnlyList<string> SourceLocators,
    string RequiredCorrection);

public sealed record InventorySourceAccountingSummary(
    int NonEmptySourceElements,
    int CommercialLookingElements,
    int AccountedCommercialElements,
    int UnaccountedCommercialElements,
    IReadOnlyDictionary<string, int> FirstFailureCounts,
    IReadOnlyDictionary<string, int> DispositionCounts);

public sealed record InventorySourceAccountingReport(
    string ProtocolVersion,
    string SourceHash,
    InventorySourceAccountingSummary Summary,
    IReadOnlyList<InventorySourceEntryTrace> Entries,
    IReadOnlyList<InventoryDeduplicationDecision> DeduplicationDecisions,
    IReadOnlyList<InventoryReviewExceptionGroup> ExceptionGroups);

public sealed record InventoryExtractedRateVariant(
    string RawValue,
    string SourceLocator,
    string HeaderHierarchy,
    IReadOnlyList<string> HeaderLocators,
    string? PositionJson = null,
    string? RateType = null,
    string? Currency = null,
    string? BuyingUnit = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null,
    string? Geography = null,
    string? Daypart = null,
    string? Days = null,
    int? DurationSeconds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Dimensions = null);
