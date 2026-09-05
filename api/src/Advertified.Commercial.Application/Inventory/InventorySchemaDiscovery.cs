namespace Advertified.Commercial.Application.Inventory;

// Versioned extraction protocol, not a second canonical commercial model.
public sealed record InventoryDocumentStructure(
    string SourceHash, string StructureHash, IReadOnlyList<InventorySourceStructure> Structures,
    IReadOnlyList<string>? ExtractionGaps = null);

public sealed record InventorySourceStructure(
    string Id, string Kind, IReadOnlyList<InventorySourceCell> Cells);

public sealed record InventorySourceCell(
    string Locator, int Row, int Column, string RawText, string? PositionJson = null);

public sealed record DiscoveredInventorySchema(
    string ProtocolVersion, string SourceHash, string StructureHash,
    IReadOnlyList<InventoryRecordSchema> Records, decimal Confidence,
    IReadOnlyList<string> Warnings, InventorySchemaProvenance Provenance,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    InventoryInterpretationCorrection? Correction = null);

public sealed record InventoryInterpretationCorrection(
    Guid ActorId, DateTimeOffset CorrectedAtUtc, string PreviousMappingRevision, string Reason);

public sealed record InventoryRecordSchema(
    string SourceStructure, InventoryRecordBoundary RecordBoundary,
    IReadOnlyList<InventorySchemaFieldMapping> FieldMappings,
    IReadOnlyList<InventorySchemaFieldMapping> SupplierMetadataMappings,
    IReadOnlyList<InventorySchemaFieldMapping> AssetMappings);

public sealed record InventoryRecordBoundary(
    int FirstRow, int LastRow, int RowsPerRecord, IReadOnlyList<int> ExcludedRows,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<int, string>? ExclusionReasons = null);

public sealed record InventorySchemaFieldMapping(
    string? CanonicalMeaning, string SourceLabel, string SourceLocation,
    string SourceStructure, int SourceColumn, int RowOffset, bool IsDocumentMetadata,
    string Interpretation, decimal Confidence, IReadOnlyList<InventorySchemaCitation> Evidence,
    string? InterpretedCode = null, string? ValueSourceLocation = null);

public sealed record InventorySchemaCitation(string SourceLocator, string QuotedText);

public sealed record InventorySchemaProvenance(
    string Interpreter, string ConfigurationVersion, string? Model, string? ProviderRequestId,
    int AiCalls, long? CostUsdMicros);

public sealed record InventoryDiscoveredField(
    string? CanonicalMeaning, string RawLabel, string RawValue, string SourceLocator,
    string SourceStructure, string? PositionJson, string Interpretation, decimal Confidence,
    string? InterpretedCode, IReadOnlyList<string> Warnings);

public sealed record InventorySchemaDiscoveryRequest(
    string ProtocolVersion, string SourceHash, string StructureHash,
    IReadOnlyList<InventorySourceStructure> RepresentativeStructures,
    IReadOnlySet<string> CanonicalMeanings,
    IReadOnlyDictionary<string, IReadOnlySet<string>> GovernedCodes,
    InventorySchemaExecutionContext? ExecutionContext = null);

public sealed record InventorySchemaExecutionContext(
    Guid TenantId, Guid ActorId, Guid ImportId, long ImportVersion, Guid AttemptId, Guid CorrelationId);

public sealed record InventorySchemaProposal(
    string ProtocolVersion, string SourceHash, string StructureHash,
    IReadOnlyList<InventoryRecordSchema> Records, decimal Confidence, IReadOnlyList<string> Warnings);

public interface IInventorySchemaInterpreter
{
    Task<DiscoveredInventorySchema> DiscoverAsync(
        InventorySchemaDiscoveryRequest request, CancellationToken cancellationToken);
}

public sealed class InventorySchemaRejectedException(string message) : Exception(message);
