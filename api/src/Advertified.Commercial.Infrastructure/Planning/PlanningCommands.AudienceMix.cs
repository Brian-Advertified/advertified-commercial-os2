using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> GenerateAudiencesOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateAudiencesCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadApprovedBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var proposal = await GetProposalAsync(brief, envelope, cancellationToken);
        if (proposal.Audiences.Count == 0 || proposal.IncrementalCostMinor != 0)
        {
            throw new InvalidOperationException("The audience proposal is invalid.");
        }
        var latest = await store.FindLatestAudienceAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var inputHash = PlanningHash.ForBrief(brief);
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.audience_definition_sets (
                id, tenant_id, brief_version_id, version_no, input_hash,
                status_code, created_by, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {briefVersionId}, {versionNumber},
                {inputHash}, {MasterDataCodes.LifecycleStatuses.Approved}, {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        foreach (var audience in proposal.Audiences)
        {
            await InsertAudienceAsync(
                envelope.TenantId, id, audience, now, cancellationToken);
        }
        var row = await store.FindLatestAudienceAsync(
            envelope.TenantId, briefVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The audience set was not persisted.");
        var view = await store.BuildAudienceViewAsync(
            envelope.TenantId, row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1, MasterDataReferences.CommercialResourceTypes.AudienceDefinitionSet,
            MasterDataReferences.CommercialActions.AudienceDefinitionsGenerated, MasterDataReferences.CommercialEventTypes.AudienceDefinitionsGenerated, now);
    }

    private async Task<CommandOutcome> GenerateMediaMixOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaMixCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadApprovedBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var audience = await store.FindLatestAudienceAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        if (audience is null || audience.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var proposal = await GetProposalAsync(brief, envelope, cancellationToken);
        EnsureAllocations(proposal, brief.BudgetMinor!.Value);
        var latest = await store.FindLatestMixAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var allocations = proposal.Allocations.Select(item => new MediaAllocationView(
            item.Channel,
            item.BudgetMinor,
            item.Role,
            item.RunningPeriods.Select(period =>
                new MediaRunningPeriodView(period.Start, period.End)).ToArray())).ToArray();
        var allocationsJson = Write(allocations);
        var rolesJson = Write(allocations.ToDictionary(item => item.Channel, item => item.Role));
        var assumptionsJson = Write(proposal.Assumptions.Concat(proposal.Unknowns).ToArray());
        var evidenceJson = brief.EvidenceIdsJson;
        var inputHash = PlanningHash.ForMix(brief, audience.Id, allocationsJson);
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.media_mix_versions (
                id, tenant_id, brief_version_id, audience_set_id, version_no,
                total_budget_minor, currency_code, allocations_json, channel_roles_json,
                assumptions_json, evidence_item_ids_json, input_hash, status_code,
                created_by, version, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {briefVersionId}, {audience.Id},
                {versionNumber}, {brief.BudgetMinor.Value}, {brief.Currency},
                {allocationsJson}::jsonb, {rolesJson}::jsonb, {assumptionsJson}::jsonb,
                {evidenceJson}::jsonb, {inputHash}, {MasterDataCodes.LifecycleStatuses.Draft},
                {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        var row = await store.FindMixAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The media mix was not persisted.");
        var view = PlanningRecordStore.BuildMixView(row);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, row.Version, MasterDataReferences.CommercialResourceTypes.MediaMixVersion,
            MasterDataReferences.CommercialActions.MediaMixGenerated, MasterDataReferences.CommercialEventTypes.MediaMixGenerated, now);
    }

    private async Task<CommandOutcome> UpdateMediaMixOutcomeAsync(
        Guid mixVersionId,
        CommandEnvelope<UpdateMediaMixCommand> envelope,
        CancellationToken cancellationToken)
    {
        var mix = await store.FindMixAsync(
            envelope.TenantId, mixVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media mix access denied.");
        var brief = await LoadApprovedBriefAsync(
            mix.BriefVersionId, envelope, cancellationToken);
        if (mix.Status != MasterDataCodes.LifecycleStatuses.Draft)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = envelope.Command.Allocations.Select(ToAllocationView).ToArray();
        EnsureAllocations(allocations, brief.BudgetMinor!.Value);
        EnsureRunningPeriods(allocations);
        var allocationsJson = Write(allocations);
        var rolesJson = Write(allocations.ToDictionary(item => item.Channel, item => item.Role));
        var inputHash = PlanningHash.ForMix(brief, mix.AudienceSetId, allocationsJson);
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.media_mix_versions
            SET allocations_json = {allocationsJson}::jsonb,
                channel_roles_json = {rolesJson}::jsonb,
                input_hash = {inputHash}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {mixVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = mix with
        {
            AllocationsJson = allocationsJson,
            InputHash = inputHash,
            Version = mix.Version + 1,
        };
        var view = PlanningRecordStore.BuildMixView(updated);
        return OpportunityCommandSupport.Outcome(
            envelope, view, mixVersionId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.MediaMixVersion,
            MasterDataReferences.CommercialActions.MediaMixUpdated,
            MasterDataReferences.CommercialEventTypes.MediaMixUpdated, now);
    }

    private async Task<CommandOutcome> ApproveMediaMixOutcomeAsync(
        Guid mixVersionId,
        CommandEnvelope<ApproveMediaMixCommand> envelope,
        CancellationToken cancellationToken)
    {
        var mix = await store.FindMixAsync(
            envelope.TenantId, mixVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media mix access denied.");
        var brief = await LoadApprovedBriefAsync(
            mix.BriefVersionId, envelope, cancellationToken);
        var allocations = Read<MediaAllocationView[]>(mix.AllocationsJson);
        EnsureAllocations(allocations, brief.BudgetMinor!.Value);
        EnsureRunningPeriods(allocations);
        if (mix.Status != MasterDataCodes.LifecycleStatuses.Draft)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.media_mix_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, approved_by = {envelope.ActorId.Value},
                approved_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {mixVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft} AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = mix with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ApprovedBy = envelope.ActorId.Value,
            Version = mix.Version + 1,
        };
        var view = PlanningRecordStore.BuildMixView(updated);
        return OpportunityCommandSupport.Outcome(
            envelope, view, mixVersionId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.MediaMixVersion, MasterDataReferences.CommercialActions.MediaMixApproved,
            MasterDataReferences.CommercialEventTypes.MediaMixApproved, now);
    }

    private async Task<PlanningBriefRow> LoadApprovedBriefAsync<TCommand>(
        Guid briefVersionId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var brief = await store.FindBriefAsync(
            envelope.TenantId, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Planning access denied.");
        if (brief.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Planning assignment denied.");
        }
        if (brief.Status != MasterDataCodes.LifecycleStatuses.Approved || brief.BudgetUnknown ||
            !brief.BudgetMinor.HasValue || string.IsNullOrWhiteSpace(brief.Currency))
        {
            throw new InvalidLifecycleTransitionException();
        }
        return brief;
    }

    private async Task<PlanningAgentProposal> GetProposalAsync<TCommand>(
        PlanningBriefRow brief,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var channels = await store.ListAvailableChannelsAsync(
            envelope.TenantId, cancellationToken);
        return await planningAgent.ProposeAsync(new PlanningBriefInput(
            envelope.TenantId.Value, envelope.ActorId.Value, brief.Id, brief.Objective,
            Read<string[]>(brief.AudiencesJson), Read<string[]>(brief.GeographiesJson),
            brief.BudgetMinor!.Value, brief.Currency!, Read<Guid[]>(brief.EvidenceIdsJson),
            channels), cancellationToken);
    }

    private Task<int> InsertAudienceAsync(
        TenantId tenantId,
        Guid setId,
        AudienceDefinitionProposal audience,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.audience_definitions (
                id, tenant_id, audience_set_id, name, description, need_state,
                buying_context, geography_json, language, life_stage, lsm_sem,
                classification_code, exclusions_json, evidence_item_ids_json,
                confidence, status_code)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {setId}, {audience.Name},
                {audience.Description}, {audience.NeedState}, {audience.BuyingContext},
                {Write(audience.Geographies)}::jsonb, {audience.Language}, {audience.LifeStage},
                {audience.LsmSem}, {audience.Classification},
                {Write(audience.Exclusions)}::jsonb, {Write(audience.EvidenceItemIds)}::jsonb,
                {audience.Confidence}, {MasterDataCodes.LifecycleStatuses.Approved})
            """, cancellationToken);

    private static void EnsureAllocations(PlanningAgentProposal proposal, long budget) =>
        EnsureAllocations(proposal.Allocations.Select(item => new MediaAllocationView(
            item.Channel,
            item.BudgetMinor,
            item.Role,
            item.RunningPeriods.Select(period =>
                new MediaRunningPeriodView(period.Start, period.End)).ToArray())).ToArray(), budget);

    private static MediaAllocationView ToAllocationView(MediaAllocationInput allocation)
    {
        var channel = OpportunityCommandSupport.Required(
            allocation.Channel, 100, nameof(allocation.Channel)).ToUpperInvariant();
        var role = OpportunityCommandSupport.Required(
            allocation.Role, 500, nameof(allocation.Role));
        return new MediaAllocationView(
            channel,
            allocation.BudgetMinor,
            role,
            allocation.RunningPeriods.Select(period =>
                new MediaRunningPeriodView(period.Start, period.End)).ToArray());
    }

    private static void EnsureAllocations(
        MediaAllocationView[] allocations,
        long budget)
    {
        if (allocations.Length == 0 || allocations.Any(item => item.BudgetMinor < 0) ||
            allocations.Select(item => item.Channel).Distinct(StringComparer.Ordinal).Count() != allocations.Length ||
            allocations.Sum(item => item.BudgetMinor) != budget)
        {
            throw new ArgumentException("Media allocations must reconcile to the planning budget.");
        }
    }

    private static void EnsureRunningPeriods(MediaAllocationView[] allocations)
    {
        foreach (var allocation in allocations)
        {
            if (allocation.RunningPeriods.Count == 0 ||
                allocation.RunningPeriods.Any(period => period.End < period.Start))
            {
                throw new ArgumentException("Each media type needs at least one valid running period.");
            }
            var ordered = allocation.RunningPeriods.OrderBy(period => period.Start).ToArray();
            if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.End >= pair.Second.Start))
            {
                throw new ArgumentException("Running periods for one media type cannot overlap.");
            }
        }
    }
}

internal static partial class PlanningHash
{
    internal static string ForBrief(PlanningBriefRow brief) =>
        OpportunityCommandSupport.Hash(
            $"{brief.Id:N}|{brief.Version}|{brief.Objective}|{brief.AudiencesJson}|" +
            $"{brief.GeographiesJson}|{brief.BudgetMinor}|{brief.Currency}|{brief.EvidenceIdsJson}");

    internal static string ForMix(
        PlanningBriefRow brief,
        Guid audienceId,
        string allocationsJson) => OpportunityCommandSupport.Hash(
            $"{ForBrief(brief)}|{audienceId:N}|{allocationsJson}");
}
