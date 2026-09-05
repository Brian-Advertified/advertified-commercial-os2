using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<CommandOutcome> GenerateOutcomeAsync(
        Guid briefId,
        CommandEnvelope<GenerateProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadOwnedPlanningReadyBriefAsync(briefId, envelope, cancellationToken);
        ValidateGenerateCommand(envelope.Command, timeProvider.GetUtcNow(), proposalPolicy);
        var plans = await LoadPlansAsync(
            envelope.TenantId, brief.BriefVersionId,
            envelope.Command.Options.Select(item => item.PlanVersionId).ToArray(),
            cancellationToken);
        var inputs = envelope.Command.Options.Zip(plans)
            .Select((pair, index) => BuildOptionInput(pair.First, pair.Second, index + 1))
            .ToArray();
        EnsureMateriallyDifferent(inputs);
        var narrative = await narrativeClient.CreateAsync(new ProposalNarrativeInput(
            envelope.TenantId.Value,
            envelope.ActorId.Value,
            envelope.CommandId.Value,
            envelope.CorrelationId.Value,
            brief.BriefVersionId,
            brief.BriefVersion,
            brief.Objective,
            ProposalRecordStore.Read<Guid[]>(brief.EvidenceIdsJson),
            inputs.Select(item => new ProposalOptionNarrativeInput(
                item.Plan.Id, item.Plan.VersionNumber,
                item.Label, item.Outcome, item.Plan.TotalMinor, item.Plan.Currency,
                item.Plan.Channels)).ToArray()), cancellationToken);
        if (narrative.IncrementalCostMinor < 0)
        {
            throw new InvalidOperationException(
                "The proposal narrative exceeded its configured provider cost policy.");
        }
        var proposalId = Guid.NewGuid();
        var versionNumber = await store.NextVersionNumberAsync(
            envelope.TenantId, briefId, cancellationToken);
        var title = OpportunityCommandSupport.Required(
            envelope.Command.Title, 300, nameof(envelope.Command.Title));
        var terms = OpportunityCommandSupport.Required(
            envelope.Command.Terms, 10_000, nameof(envelope.Command.Terms));
        var inputHash = BuildProposalHash(
            brief.BriefVersionId, title, narrative.ExecutiveSummary, inputs, terms,
            envelope.Command.ExpiryAtUtc);
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_versions (
                id, tenant_id, brief_id, brief_version_id, version_no, title,
                executive_summary, terms, expiry_at_utc, status_code, input_hash,
                agent_provider_code, agent_model_code, agent_incremental_cost_minor,
                agent_provider_request_id, created_by, version, created_at_utc)
            VALUES ({proposalId}, {envelope.TenantId.Value}, {briefId}, {brief.BriefVersionId},
                {versionNumber}, {title}, {narrative.ExecutiveSummary}, {terms},
                {envelope.Command.ExpiryAtUtc}, {MasterDataCodes.LifecycleStatuses.Draft},
                {inputHash}, {narrative.Provider}, {narrative.Model},
                {narrative.IncrementalCostMinor}, {narrative.ProviderRequestId},
                {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        await ProposalOptionPersistence.InsertAsync(
            store.DbContext, envelope.TenantId, proposalId, inputs, cancellationToken);

        var row = await store.FindProposalAsync(envelope.TenantId, proposalId, cancellationToken)
            ?? throw new InvalidOperationException("The proposal was not persisted.");
        var view = await store.BuildViewAsync(envelope.TenantId, row, cancellationToken);
        return ProposalOutcome(envelope, view, proposalId, 1,
            MasterDataReferences.CommercialActions.ProposalGenerated,
            MasterDataReferences.CommercialEventTypes.ProposalGenerated, now);
    }

    private async Task<PlanningReadyBriefReferenceRow> LoadOwnedPlanningReadyBriefAsync<TCommand>(
        Guid briefId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var brief = await store.FindPlanningReadyBriefAsync(
            envelope.TenantId, briefId, cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        if (brief.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Proposal assignment denied.");
        }
        return brief;
    }

    private async Task<ProposalPlanSnapshot[]> LoadPlansAsync(
        TenantId tenantId,
        Guid briefVersionId,
        Guid[] planIds,
        CancellationToken cancellationToken)
    {
        var uniquePlanIds = planIds.Distinct().ToArray();
        var rows = await planningStore.ListPlansAsync(
            tenantId, uniquePlanIds, cancellationToken);
        if (rows.Count != uniquePlanIds.Length)
        {
            throw new ArgumentException("A selected media plan is unavailable.");
        }
        var byId = rows.ToDictionary(item => item.Id);
        var ordered = planIds.Select(id => byId[id]).ToArray();
        if (ordered.Any(plan => plan.BriefVersionId != briefVersionId ||
                plan.Status != MasterDataCodes.LifecycleStatuses.Approved))
        {
            throw new InvalidLifecycleTransitionException();
        }
        var views = await planningStore.BuildPlanViewsAsync(
            tenantId, ordered, cancellationToken);
        var plans = views.Select(ProposalPlanProjection.ToSnapshot).ToArray();
        await inventoryReadiness.EnsurePlanInputsCurrentAsync(tenantId, plans, cancellationToken);
        return plans;
    }


    private static ProposalOptionSnapshot BuildOptionInput(
        ProposalOptionInput option,
        ProposalPlanSnapshot plan,
        int displayOrder) => new(
            OpportunityCommandSupport.Required(option.Label, 200, nameof(option.Label)),
            OpportunityCommandSupport.Required(option.Outcome, 2_000, nameof(option.Outcome)),
            plan, displayOrder);

    private static void EnsureMateriallyDifferent(ProposalOptionSnapshot[] options)
    {
        if (options.Select(item => item.Plan.Id).Distinct().Count() != options.Length ||
            options.Select(item => item.Plan.Signature).Distinct(StringComparer.Ordinal).Count() != options.Length)
        {
            throw new ArgumentException("Proposal choices must reference genuinely different approved plans.");
        }
    }

    private static void ValidateGenerateCommand(
        GenerateProposalCommand command,
        DateTimeOffset now,
        ProposalPolicy policy)
    {
        var maximumExpiry = now.AddDays(policy.MaximumValidityDays);
        if (command.Options.Count < policy.MinimumOptions ||
            command.Options.Count > policy.MaximumOptions ||
            command.ExpiryAtUtc <= now || command.ExpiryAtUtc > maximumExpiry)
        {
            throw new ArgumentException("The proposal choices or expiry are outside the account policy.");
        }
    }


    private static string BuildProposalHash(
        Guid briefVersionId,
        string title,
        string executiveSummary,
        IReadOnlyList<ProposalOptionSnapshot> options,
        string terms,
        DateTimeOffset expiry) => OpportunityCommandSupport.Hash(
            $"{briefVersionId:N}|{title}|{executiveSummary}|" +
            $"{string.Join('|', options.Select(item => $"{item.Plan.Signature}:{item.Label}:{item.Outcome}"))}|" +
            $"{terms}|{expiry:O}");
}

internal sealed record ProposalPlanSnapshot(
    Guid Id,
    int VersionNumber,
    long TotalMinor,
    string Currency,
    string InputHash,
    IReadOnlyList<string> Channels,
    IReadOnlyList<ProposalRunningPeriodView> Periods,
    IReadOnlyList<string> InventoryNames,
    IReadOnlyList<ProposalInventoryLineView> Inventory,
    string Signature,
    IReadOnlyList<ProposalPlanLineReference> Lines);

internal sealed record ProposalPlanLineReference(
    Guid InventoryTenantId,
    Guid? MarketplaceListingVersionId,
    Guid InventoryProductId,
    Guid ProductVersionId,
    Guid RateId,
    Guid? AvailabilityId,
    IReadOnlyList<ProposalRunningPeriodView> RunningPeriods);

internal sealed record ProposalOptionSnapshot(
    string Label,
    string Outcome,
    ProposalPlanSnapshot Plan,
    int DisplayOrder);
