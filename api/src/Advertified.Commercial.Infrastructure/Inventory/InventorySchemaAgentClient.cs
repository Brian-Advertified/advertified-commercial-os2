using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySchemaAgentClient(HttpClient client, InventorySemanticStore store,
    InventoryAgentInvocationLedger ledger, IOptions<AgentRuntimeOptions> runtime,
    IOptions<InventorySemanticOptions> budget) : IInventorySchemaInterpreter
{
    internal const string Operation = "INVENTORY_SCHEMA_DISCOVERY";
    internal const string PromptVersion = "inventory-schema-prompt/1.1";

    public async Task<DiscoveredInventorySchema> DiscoverAsync(
        InventorySchemaDiscoveryRequest request, CancellationToken cancellationToken)
    {
        var execution = request.ExecutionContext ?? throw new InventorySchemaRejectedException("Schema discovery requires an authorized import context.");
        var settings = runtime.Value;
        var limits = budget.Value;
        if (!limits.Enabled || !InventorySemanticOptions.IsPlanningValid(limits) || !settings.UsesHttp || !settings.AllowLive)
            throw new InventorySchemaRejectedException("Semantic schema discovery is not enabled. Source evidence requires schema review.");
        InventorySemanticEnrichmentService.EnsureLiveConfiguration(settings, limits);
        var context = new InventorySemanticContext(execution.TenantId, execution.ActorId, execution.AttemptId,
            execution.CorrelationId, execution.ImportId, execution.ImportVersion, request.SourceHash, string.Empty, string.Empty);
        var document = new { request.ProtocolVersion, request.SourceHash, request.StructureHash,
            request.RepresentativeStructures,
            CanonicalMeanings = request.CanonicalMeanings.Order(StringComparer.Ordinal).ToArray(),
            GovernedCodes = request.GovernedCodes.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value.Order(StringComparer.Ordinal).ToArray()),
        };
        var json = JsonSerializer.Serialize(document, AgentRuntimeHttpSupport.WireJson);
        if (json.Length > limits.MaximumChunkCharacters)
            throw new InventorySchemaRejectedException("Schema samples exceed the configured request budget; review or split the source structures.");
        var hash = CommandPayloadDigest.Create(new { Operation, document,
            PromptVersion, settings.Provider, Model = settings.ModelFor(MasterDataCodes.AgentTypes.InventoryIntelligence),
            execution.TenantId, execution.ActorId, execution.ImportId, execution.ImportVersion }).Value;
        var packet = new InventorySemanticPacket(Guid.NewGuid(), Operation, 1, 1, hash, json, [], [], [], limits.PerCallCostCapUsdMicros);
        InventorySemanticBudgetPolicy.Ensure([packet], limits);
        var runs = await store.PrepareAsync(context, execution.AttemptId, [packet],
            settings.ModelFor(MasterDataCodes.AgentTypes.InventoryIntelligence), PromptVersion,
            limits.BudgetScope, limits.CertificationBudgetUsdMicros, cancellationToken);
        var response = await ledger.InvokeAsync(context, runs.Single(), packet.MaximumCostUsdMicros,
            token => InvokeAsync(context, packet, document, token), cancellationToken);
        var result = response.Artifact ?? throw new InventorySchemaRejectedException("Schema interpretation returned no artifact.");
        return new DiscoveredInventorySchema(result.ProtocolVersion, result.SourceHash, result.StructureHash,
            result.Records, result.Confidence, result.Warnings, new InventorySchemaProvenance(
                settings.Provider, PromptVersion,
                settings.ModelFor(MasterDataCodes.AgentTypes.InventoryIntelligence), response.Usage.ProviderRequestId,
                1, response.Usage.IncrementalCostUsdMicros));
    }

    private Task<AgentRuntimeResponse<InventorySchemaProposal>> InvokeAsync(InventorySemanticContext context,
        InventorySemanticPacket packet, object document, CancellationToken cancellationToken)
    {
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(context.TenantId, context.ActorId, context.RunId,
            packet.StepId, context.CorrelationId, MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataReferences.CommercialResourceTypes.InventoryImport.Value, context.ImportId, context.ImportVersion, [], runtime.Value);
        return AgentRuntimeHttpSupport.InvokeAsync<InventorySchemaProposal>(client, runtime.Value,
            MasterDataCodes.AgentTypes.InventoryIntelligence, new { operation = Operation, invocation, document }, [], cancellationToken);
    }
}
