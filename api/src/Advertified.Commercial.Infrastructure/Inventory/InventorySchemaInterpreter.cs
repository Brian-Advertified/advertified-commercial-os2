using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySchemaInterpreter(
    InventorySemanticStore store,
    InventorySemanticAgentClient client,
    IOptions<InventorySemanticOptions> semanticOptions,
    IOptions<AgentRuntimeOptions> runtimeOptions) : IInventorySchemaInterpreter
{
    private const string AgentOperation = "INVENTORY_SCHEMA_DISCOVERY";

    public async Task<DiscoveredInventorySchema> DiscoverAsync(
        InventorySchemaDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var execution = request.ExecutionContext ??
            throw new InventorySchemaRejectedException("Schema execution context is required.");
        var semantic = semanticOptions.Value;
        var runtime = runtimeOptions.Value;
        if (!semantic.Enabled)
            throw new InventorySchemaRejectedException("Schema discovery is not enabled.");
        InventorySemanticConfigurationGuard.EnsureLive(runtime, semantic);
        var payload = BuildPayload(request, execution, runtime, semantic.PromptVersion);
        var requestJson = JsonSerializer.Serialize(payload, AgentRuntimeHttpSupport.WireJson);
        var packet = new InventorySemanticPacket(
            execution.AttemptId,
            InventoryExtractionTraceCodes.SchemaDiscovery,
            1,
            1,
            InventoryExtractionContract.Hash(requestJson),
            requestJson,
            [],
            [],
            [],
            semantic.MaximumCostUsdMicros(requestJson.Length, 0));
        InventorySemanticBudgetPolicy.Ensure([packet], semantic);
        var context = new InventorySemanticContext(
            execution.TenantId,
            execution.ActorId,
            execution.AttemptId,
            execution.CorrelationId,
            execution.ImportId,
            execution.ImportVersion,
            request.SourceHash);
        var runs = await store.PrepareAsync(
            context,
            execution.AttemptId,
            [packet],
            runtime.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                InventoryExtractionTraceCodes.SchemaDiscovery),
            semantic.PromptVersion,
            semantic.BudgetScope,
            semantic.CertificationBudgetUsdMicros,
            cancellationToken);
        var run = runs.Single();
        var response = run.Status == MasterDataCodes.LifecycleStatuses.Completed
            ? InventorySemanticStore.ReadResponse<InventorySchemaProposal>(run)
            : await InvokeAsync(context, run, payload, cancellationToken);
        InventorySchemaProjection.Validate(
            new InventoryDocumentStructure(
                request.SourceHash,
                request.StructureHash,
                request.RepresentativeStructures),
            response.Artifact ?? throw new InventorySchemaRejectedException(
                "Schema discovery returned no artifact."),
            request.GovernedCodes);
        return ToDiscovered(response, semantic.PromptVersion);
    }

    private async Task<AgentRuntimeResponse<InventorySchemaProposal>> InvokeAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        InventorySchemaAgentRequest payload,
        CancellationToken cancellationToken)
    {
        if (run.Status != MasterDataCodes.LifecycleStatuses.Pending)
            throw new InventorySemanticReconciliationRequiredException();
        await store.MarkRunningAsync(context, run, cancellationToken);
        try
        {
            var response = await client.InvokeSchemaAsync(payload, cancellationToken);
            await store.MarkCompletedAsync(context, run, response, cancellationToken);
            return response;
        }
        catch (AgentRuntimeRejectedException rejected)
        {
            await store.MarkRejectedAsync(context, run, rejected, CancellationToken.None);
            if (rejected.HasDefinitiveProviderAcceptance)
                throw new InventorySemanticResultRejectedException(rejected.Stage);
            throw new InventorySemanticReconciliationRequiredException();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await store.MarkReconciliationRequiredAsync(
                context, run, "SCHEMA_CALL_INTERRUPTED", CancellationToken.None);
            throw;
        }
        catch
        {
            await store.MarkReconciliationRequiredAsync(
                context, run, "SCHEMA_RESULT_NOT_ACCEPTED", CancellationToken.None);
            throw new InventorySemanticReconciliationRequiredException();
        }
    }

    private static InventorySchemaAgentRequest BuildPayload(
        InventorySchemaDiscoveryRequest request,
        InventorySchemaExecutionContext execution,
        AgentRuntimeOptions runtime,
        string promptVersion)
    {
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            execution.TenantId,
            execution.ActorId,
            execution.AttemptId,
            execution.AttemptId,
            execution.CorrelationId,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataReferences.CommercialResourceTypes.InventoryImport.Value,
            execution.ImportId,
            execution.ImportVersion,
            [],
            runtime,
            promptVersion,
            InventoryExtractionTraceCodes.SchemaDiscovery);
        return new InventorySchemaAgentRequest(
            AgentOperation,
            invocation,
            new InventorySchemaAgentDocument(
                request.ProtocolVersion,
                request.SourceHash,
                request.StructureHash,
                request.RepresentativeStructures,
                request.CanonicalMeanings.Order(StringComparer.Ordinal).ToArray(),
                request.GovernedCodes.ToDictionary(
                    item => item.Key,
                    item => item.Value.Order(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal)));
    }

    private static DiscoveredInventorySchema ToDiscovered(
        AgentRuntimeResponse<InventorySchemaProposal> response,
        string promptVersion)
    {
        var artifact = response.Artifact ??
            throw new InventorySchemaRejectedException("Schema discovery returned no artifact.");
        return new DiscoveredInventorySchema(
            artifact.ProtocolVersion,
            artifact.SourceHash,
            artifact.StructureHash,
            artifact.Records,
            artifact.Confidence,
            artifact.Warnings,
            new InventorySchemaProvenance(
                "inventory-schema-discovery",
                promptVersion,
                response.Usage.Model,
                response.Usage.ProviderRequestId,
                1,
                response.Usage.IncrementalCostUsdMicros));
    }
}

internal sealed record InventorySchemaAgentRequest(
    string Operation,
    AgentInvocationRequest Invocation,
    InventorySchemaAgentDocument Document);

internal sealed record InventorySchemaAgentDocument(
    string ProtocolVersion,
    string SourceHash,
    string StructureHash,
    IReadOnlyList<InventorySourceStructure> RepresentativeStructures,
    IReadOnlyList<string> CanonicalMeanings,
    IReadOnlyDictionary<string, string[]> GovernedCodes);
