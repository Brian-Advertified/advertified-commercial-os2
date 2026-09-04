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
        var settings = semanticOptions.Value;
        if (!settings.Enabled)
            return extraction;

        var runtime = runtimeOptions.Value;
        EnsureLiveConfiguration(runtime, settings);
        var (source, codes, sourceContent) =
            await LoadSourceAsync(claim, cancellationToken);
        var context = CreateContext(claim, source);
        var request = new InventoryExtractionRequest(
            source.FileName,
            source.DeclaredMediaType,
            context.DocumentClass,
            source.SourceHash,
            sourceContent);

        if (NativeOfficeImageReader.IsRequired(extraction.Rows))
        {
            // Local OCR could not safely establish source rows. Paid AI is
            // never used as a fallback source of commercial truth.
            return extraction;
        }

        var enrichmentPackets = InventorySemanticPacketBuilder
            .BuildEnrichment(
                request,
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
        if (packets.Any(packet => packet.Operation !=
                InventorySemanticOperations.SemanticEnrichment))
        {
            throw new InventorySemanticReconciliationRequiredException();
        }
        return InventorySemanticMerger.Merge(
            sourceRows,
            packets,
            results,
            codes);
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
            rows);

    private static InventorySemanticContext CreateContext(
        InventoryExtractionWorkerClaim claim,
        InventoryImportRow source) => new(
        claim.TenantId,
        claim.RequestedBy,
        claim.AttemptId,
        claim.CorrelationId,
        claim.ImportId,
        source.Version,
        claim.SourceHash,
        source.FileName,
        source.DocumentClass ??
            throw new InvalidOperationException(
                "The inventory document class is absent."));

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
                MasterDataCodes.AgentTypes.InventoryIntelligence),
            settings.PromptVersion,
            settings.BudgetScope,
            settings.CertificationBudgetUsdMicros,
            cancellationToken);

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
            return InventorySemanticStore.ReadResponse(run);
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
        InventoryCodeSets Codes,
        byte[] Content)> LoadSourceAsync(
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
        return (source, codes, content);
    }

    private static InventorySemanticCodes ToCodes(
        InventoryCodeSets codes) => new(
        codes.Channels.Order(StringComparer.Ordinal).ToArray(),
        codes.ProductTypes.Order(StringComparer.Ordinal).ToArray(),
        codes.RateTypes.Order(StringComparer.Ordinal).ToArray(),
        codes.Currencies.Order(StringComparer.Ordinal).ToArray(),
        codes.Availability.Order(StringComparer.Ordinal).ToArray());

    private static void EnsureLiveConfiguration(
        AgentRuntimeOptions runtime,
        InventorySemanticOptions semantic)
    {
        var expectedCapMinor =
            (semantic.PerCallCostCapUsdMicros + 9_999L) /
            10_000L;
        if (!InventorySemanticOptions.IsPlanningValid(semantic) ||
            runtime.Mode != AgentRuntimeOptions.HttpMode ||
            runtime.Provider !=
                AgentRuntimeOptions.BedrockProvider ||
            !runtime.AllowLive ||
            !string.Equals(
                runtime.ModelFor(
                    MasterDataCodes.AgentTypes
                        .InventoryIntelligence),
                semantic.ModelId,
                StringComparison.Ordinal) ||
            runtime.CostCapFor(
                MasterDataCodes.AgentTypes
                    .InventoryIntelligence) !=
                expectedCapMinor)
        {
            throw new InvalidOperationException(
                "Semantic extraction requires the exact governed and " +
                "preflighted live route.");
        }
    }
}
