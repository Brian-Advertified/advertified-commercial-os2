using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySemanticEnrichmentService(
    InventoryRecordStore inventoryStore,
    InventorySemanticStore semanticStore,
    InventorySemanticAgentClient agentClient,
    IOptions<InventorySemanticOptions> semanticOptions,
    IOptions<AgentRuntimeOptions> runtimeOptions)
{
    internal string CurrentProjectionVersion =>
        InventoryProjectionVersion.Current(
            semanticOptions.Value);

    internal async Task<InventoryExtractionResult> EnrichAsync(
        InventoryExtractionWorkerClaim claim,
        InventoryExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        if (extraction.Document.DiscoveredSchema is not null || extraction.Document.SchemaDiscoveryFailure is not null)
            return extraction;
        var settings = semanticOptions.Value;
        if (!settings.Enabled)
            return extraction;

        var runtime = runtimeOptions.Value;
        InventorySemanticConfigurationGuard.EnsureLive(runtime, settings);
        var (source, codes) =
            await LoadSourceAsync(claim, cancellationToken);
        var context = CreateContext(claim, source);

        if (extraction.Rows.Count == 0)
        {
            var transcriptionPackets = InventorySemanticPacketBuilder
                .BuildTranscription(extraction, codes, settings);
            InventorySemanticBudgetPolicy.Ensure(
                transcriptionPackets, settings);
            var transcribedRows = await ExecuteStageAsync(
                context,
                claim.AttemptId,
                [],
                transcriptionPackets,
                codes,
                runtime,
                settings,
                cancellationToken);
            extraction = CreateResult(extraction, transcribedRows);
        }

        var enrichmentPackets = InventorySemanticPacketBuilder
            .BuildEnrichment(
                extraction,
                codes,
                settings);
        if (enrichmentPackets.Count == 0)
            return extraction;
        try
        {
            InventorySemanticBudgetPolicy.Ensure(
                enrichmentPackets, settings);
            var enrichedRows = await ExecuteStageAsync(
                context,
                claim.AttemptId,
                extraction.Rows,
                enrichmentPackets,
                codes,
                runtime,
                settings,
                cancellationToken);
            return CreateResult(
                extraction, enrichedRows);
        }
        catch (Exception error) when (
            error is InventorySemanticBudgetExceededException or
                InventorySemanticResultRejectedException or
                InventorySemanticReconciliationRequiredException)
        {
            // A rejected enrichment may never erase accepted source facts.
            return extraction;
        }
    }

    private async Task<IReadOnlyList<InventoryExtractedRow>>
        ExecuteStageAsync(
            InventorySemanticContext context,
            Guid attemptId,
            IReadOnlyList<InventoryExtractedRow> sourceRows,
            IReadOnlyList<InventorySemanticPacket> packets,
            InventoryCodeSets codes,
            AgentRuntimeOptions runtime,
            InventorySemanticOptions settings,
            CancellationToken cancellationToken)
    {
        if (packets.Count == 0)
            return sourceRows;
        var runs = await PrepareRunsAsync(
            context,
            attemptId,
            packets,
            runtime,
            settings,
            cancellationToken);
        var results = await RunPacketsAsync(
            context,
            packets,
            runs,
            codes,
            cancellationToken);
        var operation = RequireSingleOperation(packets);
        return operation == InventorySemanticOperations.SourceTranscription
            ? InventorySemanticTranscriptionMerger.Merge(packets, results)
            : InventorySemanticMerger.Merge(
                sourceRows, packets, results, codes);
    }

    private InventoryExtractionResult CreateResult(
        InventoryExtractionResult extraction,
        IReadOnlyList<InventoryExtractedRow> rows) =>
        InventoryExtractionContract.Create(
            extraction.AdapterCode,
            CurrentProjectionVersion,
            extraction.SchemaVersion,
              extraction.SourceHash,
              extraction.ProviderJson,
              rows,
              extraction.Document.DiscoveredSchema,
              extraction.Document.SchemaDiscoveryFailure,
              deduplicationDecisions:
                  extraction.Document.DeduplicationDecisions,
              sourceElements: extraction.Document.SourceElements,
              projectionWarnings:
                  extraction.Document.ProjectionWarnings,
              sourceImages: extraction.Document.SourceImages);

    private static InventorySemanticContext CreateContext(
        InventoryExtractionWorkerClaim claim,
        InventoryImportRow source) => new(
        claim.TenantId,
        claim.RequestedBy,
        claim.AttemptId,
        claim.CorrelationId,
        claim.ImportId,
        source.Version,
        claim.SourceHash);

    private Task<IReadOnlyList<InventorySemanticRunRow>>
        PrepareRunsAsync(
            InventorySemanticContext context,
            Guid attemptId,
            IReadOnlyList<InventorySemanticPacket> packets,
            AgentRuntimeOptions runtime,
            InventorySemanticOptions settings,
            CancellationToken cancellationToken) =>
        semanticStore.PrepareAsync(
            context,
            attemptId,
            packets,
            runtime.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                RequireSingleOperation(packets)),
            settings.PromptVersion,
            settings.BudgetScope,
            settings.CertificationBudgetUsdMicros,
            cancellationToken);

    private static string RequireSingleOperation(
        IReadOnlyList<InventorySemanticPacket> packets)
    {
        var operations = packets.Select(packet => packet.Operation)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return operations.Length == 1
            ? operations[0]
            : throw new InvalidOperationException(
                "One semantic run batch must use one model operation.");
    }

    private async Task<IReadOnlyList<AgentSemanticResult>>
        RunPacketsAsync(
            InventorySemanticContext context,
            IReadOnlyList<InventorySemanticPacket> packets,
            IReadOnlyList<InventorySemanticRunRow> runs,
            InventoryCodeSets codes,
            CancellationToken cancellationToken)
    {
        if (runs.Count != packets.Count)
        {
            throw new
                InventorySemanticReconciliationRequiredException();
        }
        var results = new List<AgentSemanticResult>();
        for (var index = 0; index < packets.Count; index++)
        {
            var response = await ResolvePacketAsync(
                context,
                packets[index],
                runs[index],
                codes,
                cancellationToken);
            results.Add(new AgentSemanticResult(
                packets[index].InputHash,
                response));
        }
        return results;
    }

    private async Task<AgentRuntimeResponse<
        InventorySemanticExtractionArtifact>> ResolvePacketAsync(
            InventorySemanticContext context,
            InventorySemanticPacket packet,
            InventorySemanticRunRow run,
            InventoryCodeSets codes,
            CancellationToken cancellationToken)
    {
        if (run.Status ==
            MasterDataCodes.LifecycleStatuses.Completed)
        {
            return InventorySemanticStore.ReadResponse<InventorySemanticExtractionArtifact>(run);
        }
        if (run.Status !=
            MasterDataCodes.LifecycleStatuses.Pending)
        {
            throw new
                InventorySemanticReconciliationRequiredException();
        }

        await semanticStore.MarkRunningAsync(
            context, run, cancellationToken);
        return await InvokePendingAsync(
            context,
            packet,
            run,
            codes,
            cancellationToken);
    }

    private async Task<AgentRuntimeResponse<
        InventorySemanticExtractionArtifact>> InvokePendingAsync(
            InventorySemanticContext context,
            InventorySemanticPacket packet,
            InventorySemanticRunRow run,
            InventoryCodeSets codes,
            CancellationToken cancellationToken)
    {
        try
        {
            var response = await agentClient.InvokeAsync(
                context,
                packet,
                ToCodes(codes),
                cancellationToken);
            if (response.Usage.IncrementalCostUsdMicros >
                packet.MaximumCostUsdMicros)
            {
                throw new InvalidOperationException(
                    "Semantic extraction exceeded its reserved cost.");
            }
            await semanticStore.MarkCompletedAsync(
                context,
                run,
                response,
                cancellationToken);
            return response;
        }
        catch (AgentRuntimeRejectedException rejected)
        {
            await semanticStore.MarkRejectedAsync(
                context,
                run,
                rejected,
                CancellationToken.None);
            if (rejected.HasDefinitiveProviderAcceptance)
            {
                throw new InventorySemanticResultRejectedException(
                    rejected.Stage);
            }
            throw new
                InventorySemanticReconciliationRequiredException();
        }
        catch (AiMonthlyBudgetExceededException)
        {
            await semanticStore.MarkBudgetRejectedAsync(context, run, CancellationToken.None);
            throw new InventorySemanticBudgetExceededException();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await MarkAmbiguousAsync(
                context,
                run,
                "SEMANTIC_CALL_INTERRUPTED");
            throw;
        }
        catch
        {
            await MarkAmbiguousAsync(
                context,
                run,
                "SEMANTIC_RESULT_NOT_ACCEPTED");
            throw new
                InventorySemanticReconciliationRequiredException();
        }
    }

    private Task MarkAmbiguousAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        string reason) =>
        semanticStore.MarkReconciliationRequiredAsync(
            context,
            run,
            reason,
            CancellationToken.None);

    private async Task<(InventoryImportRow Source,
        InventoryCodeSets Codes)> LoadSourceAsync(
            InventoryExtractionWorkerClaim claim,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await inventoryStore.BeginSessionAsync(
                new ActorId(claim.RequestedBy),
                new TenantId(claim.TenantId),
                cancellationToken);
        var source = await inventoryStore.FindImportAsync(
            new TenantId(claim.TenantId),
            claim.ImportId,
            false,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Inventory import access denied.");
        var codes = await InventoryCodeSets.LoadAsync(
            inventoryStore.DbContext,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(
                source.ProtectedObjectKey))
        {
            throw new InventorySemanticInputRejectedException();
        }
        var content = await inventoryStore.ObjectStore.ReadAsync(
            source.ProtectedObjectKey,
            cancellationToken);
        InventoryExtractionCompletionPolicy.VerifySource(
            content, claim.SourceHash);
        return (source, codes);
    }

    private static InventorySemanticCodes ToCodes(
        InventoryCodeSets codes) => new(
        codes.Channels.Order(StringComparer.Ordinal).ToArray(),
        codes.ProductTypes.Order(StringComparer.Ordinal).ToArray(),
        codes.RateTypes.Order(StringComparer.Ordinal).ToArray(),
        codes.Currencies.Order(StringComparer.Ordinal).ToArray(),
        codes.Availability.Order(StringComparer.Ordinal).ToArray());
}
