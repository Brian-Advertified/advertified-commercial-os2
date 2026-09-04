using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySemanticAgentClient(
    HttpClient client,
    IOptions<AgentRuntimeOptions> options)
{
    internal Task<AgentRuntimeResponse<
        InventorySemanticExtractionArtifact>> InvokeAsync(
        InventorySemanticContext context,
        InventorySemanticPacket packet,
        InventorySemanticCodes codes,
        CancellationToken cancellationToken)
    {
        if (!InventorySemanticOperations.IsSupported(packet.Operation))
        {
            throw new InvalidOperationException(
                "The inventory semantic operation is not supported.");
        }
        var settings = options.Value;
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            context.TenantId,
            context.ActorId,
            context.RunId,
            packet.StepId,
            context.CorrelationId,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataReferences.CommercialResourceTypes.InventoryImport.Value,
            context.ImportId,
            context.ImportVersion,
            [],
            settings);
        var payload = new InventorySemanticAgentRequest(
            packet.Operation,
            invocation,
            context.SourceHash,
            context.FileName,
            context.DocumentClass,
            packet.Number,
            packet.Count,
            packet.SourceItems,
            packet.ExistingRows,
            packet.Images,
            codes);
        return AgentRuntimeHttpSupport.InvokeAsync<
            InventorySemanticExtractionArtifact>(
            client,
            settings,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            payload,
            [],
            cancellationToken);
    }
}

internal sealed record InventorySemanticContext(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid CorrelationId,
    Guid ImportId,
    long ImportVersion,
    string SourceHash,
    string FileName,
    string DocumentClass);

internal sealed record InventorySemanticAgentRequest(
    string Operation,
    AgentInvocationRequest Invocation,
    string SourceHash,
    string FileName,
    string DocumentClass,
    int ChunkNumber,
    int ChunkCount,
    IReadOnlyList<InventorySemanticSourceItem> SourceItems,
    IReadOnlyList<InventorySemanticExistingRow> ExistingRows,
    IReadOnlyList<InventorySemanticImage> SourceImages,
    InventorySemanticCodes GovernedCodes);

internal sealed record InventorySemanticPacket(
    Guid StepId,
    string Operation,
    int Number,
    int Count,
    string InputHash,
    string RequestJson,
    IReadOnlyList<InventorySemanticSourceItem> SourceItems,
    IReadOnlyList<InventorySemanticExistingRow> ExistingRows,
    IReadOnlyList<InventorySemanticImage> Images,
    long MaximumCostUsdMicros);

internal sealed record InventorySemanticSourceItem(
    string Locator,
    string Kind,
    string Content,
    decimal? Confidence);

internal sealed record InventorySemanticExistingRow(
    int RowNumber,
    string Locator,
    IReadOnlyDictionary<string, string> Values);

internal sealed record InventorySemanticCodes(
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> ProductTypes,
    IReadOnlyList<string> RateTypes,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<string> AvailabilityStatuses);

internal sealed record InventorySemanticExtractionArtifact(
    IReadOnlyList<ProposedInventoryCandidate> Candidates,
    IReadOnlyList<string> OmittedSourceLocators);

internal sealed record ProposedInventoryCandidate(
    string SourceLocator,
    IReadOnlyList<ProposedInventoryField> Fields,
    IReadOnlyList<string> AmbiguityNotes);

internal sealed record ProposedInventoryField(
    string FieldName,
    string RawValue,
    string? NormalizedValue,
    string SourceLocator,
    string EvidenceBasis,
    string Transformation,
    decimal Confidence);
