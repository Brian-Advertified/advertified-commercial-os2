using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySemanticPreflightReader(
    InventoryRecordStore store,
    ITenantAuthorizer authorizer,
    IOptions<InventorySemanticOptions> semanticOptions,
    IOptions<AgentRuntimeOptions> runtimeOptions) :
    IInventorySemanticPreflightReader
{
    public async Task<InventorySemanticPreflightView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid? importId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, cancellationToken);
        var settings = semanticOptions.Value;
        var runtime = runtimeOptions.Value;
        var version =
            InventoryProjectionVersion.Planned(settings);
        var model = settings.ModelId;
        var configurationBlockers =
            ConfigurationBlockers(settings, runtime);
        var (sources, codes) = await LoadSourcesAsync(
            actorId, tenantId, importId, version,
            cancellationToken);
        if (importId.HasValue && sources.Count == 0)
            throw new UnauthorizedAccessException(
                "Inventory import access denied.");

        var canPlan = !configurationBlockers.Contains(
            "SEMANTIC_PLAN_CONFIGURATION_INVALID",
            StringComparer.Ordinal);
        var plans = await PlanSourcesAsync(
            sources, codes, settings, canPlan,
            cancellationToken);
        var budget = await LoadBudgetAsync(
            actorId, tenantId, plans, settings,
            model, cancellationToken);
        var cap = settings.PerCallCostCapUsdMicros;
        var views = plans.Select(plan =>
            ToView(plan, budget.Runs, cap)).ToArray();
        var blockers = ReleaseBlockers(
            configurationBlockers, views,
            budget, settings);
        var worstCase = checked(
            budget.ExistingCommittedUsdMicros +
            budget.NewMaximumUsdMicros);
        return new InventorySemanticPreflightView(
            version,
            AgentRuntimeOptions.BedrockProvider,
            model,
            settings.PromptVersion,
            settings.BudgetScope,
            settings.InputPricePerMillionTokensUsdMicros,
            settings.OutputPricePerMillionTokensUsdMicros,
            cap,
            settings.CertificationBudgetUsdMicros,
            budget.ExistingCommittedUsdMicros,
            budget.NewMaximumUsdMicros,
            worstCase,
            IsLiveEnabled(settings, runtime),
            blockers.Length == 0,
            blockers,
            views);
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.InventoryImport,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException(
                "Inventory access denied.");
    }

    private async Task<(List<SemanticPreflightSourceRow>,
        InventoryCodeSets)> LoadSourcesAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid? importId,
        string projectionVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await store.BeginSessionAsync(
                actorId, tenantId, cancellationToken);
        var sources =
            await store.ListSemanticPreflightSourcesAsync(
                tenantId, importId, projectionVersion,
                cancellationToken);
        var codes = await InventoryCodeSets.LoadAsync(
            store.DbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (sources, codes);
    }

    private async Task<IReadOnlyList<PlannedSemanticSource>>
        PlanSourcesAsync(
            IReadOnlyList<SemanticPreflightSourceRow> sources,
            InventoryCodeSets codes,
            InventorySemanticOptions settings,
            bool canPlan,
            CancellationToken cancellationToken)
    {
        var result = new List<PlannedSemanticSource>();
        foreach (var source in sources)
        {
            if (!canPlan)
            {
                result.Add(new(
                    source, [], "SEMANTIC_PLAN_CONFIGURATION_INVALID"));
                continue;
            }
            result.Add(await PlanSourceAsync(
                source, codes, settings,
                cancellationToken));
        }
        return result;
    }

    private async Task<PlannedSemanticSource> PlanSourceAsync(
        SemanticPreflightSourceRow source,
        InventoryCodeSets codes,
        InventorySemanticOptions settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                source.ProtectedObjectKey) ||
            string.IsNullOrWhiteSpace(source.DocumentClass))
        {
            return new(source, [], "SOURCE_METADATA_INCOMPLETE");
        }
        try
        {
            var content = await store.ObjectStore.ReadAsync(
                source.ProtectedObjectKey, cancellationToken);
            InventoryExtractionCompletionPolicy.VerifySource(
                content, source.SourceHash);
            var request = new InventoryExtractionRequest(
                source.FileName, source.MediaType,
                source.DocumentClass, source.SourceHash,
                content);
            var rows = DoclingInventoryProjection.ReadRows(
                request, source.ProviderJson);
            var provider = InventoryExtractionContract.Create(
                "docling",
                InventoryExtractionOptions.PinnedAdapterVersion,
                InventoryExtractionOptions.CurrentSchemaVersion,
                source.SourceHash,
                source.ProviderJson,
                rows);
            var extraction =
                NativeOfficeInventoryProjection.Apply(
                    request, provider);
            if (NativeOfficeImageReader.IsRequired(extraction.Rows))
            {
                return new(
                    source,
                    [],
                    "LOCAL_IMAGE_OCR_REQUIRED");
            }
            var packets = InventorySemanticPacketBuilder
                .BuildEnrichment(
                    request, extraction, codes, settings)
                .Where(packet =>
                    packet.ExistingRows.Count > 0)
                .ToArray();
            return new(source, packets, null);
        }
        catch (InventoryProtectionUnavailableException)
        {
            return new(source, [], "SOURCE_HASH_MISMATCH");
        }
        catch (InventorySemanticInputRejectedException)
        {
            return new(source, [], "SEMANTIC_INPUT_NOT_SUPPORTED");
        }
        catch (Exception error) when (
            error is InventoryExtractionUnavailableException or
                JsonException)
        {
            return new(source, [], "RETAINED_ARTIFACT_INVALID");
        }
        catch (InvalidOperationException)
        {
            return new(source, [], "SEMANTIC_PLAN_LIMIT_EXCEEDED");
        }
    }

    private async Task<SemanticBudgetPreflight> LoadBudgetAsync(
        ActorId actorId,
        TenantId tenantId,
        IReadOnlyList<PlannedSemanticSource> plans,
        InventorySemanticOptions settings,
        string model,
        CancellationToken cancellationToken)
    {
        var hashes = plans
            .SelectMany(plan => plan.Packets)
            .Select(packet => packet.InputHash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (hashes.Length == 0 ||
            string.IsNullOrWhiteSpace(settings.BudgetScope))
            return new([], 0, 0);

        await using var transaction =
            await store.BeginSessionAsync(
                actorId, tenantId, cancellationToken);
        var runs = await store.ListSemanticPreflightRunsAsync(
            tenantId, hashes, model,
            settings.PromptVersion, cancellationToken);
        var existing = await store.ReadSemanticCommittedCostAsync(
            tenantId, settings.BudgetScope,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var known = runs.Select(run => run.InputHash)
            .ToHashSet(StringComparer.Ordinal);
        var additional = plans
            .SelectMany(plan => plan.Packets)
            .Where(packet => !known.Contains(packet.InputHash))
            .GroupBy(packet => packet.InputHash, StringComparer.Ordinal)
            .Sum(group => group.First().MaximumCostUsdMicros);
        return new(runs, existing, additional);
    }

}
