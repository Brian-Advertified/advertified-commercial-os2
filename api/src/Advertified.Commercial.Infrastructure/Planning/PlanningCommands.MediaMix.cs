using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> UpdateMediaMixOutcomeAsync(
        Guid mixVersionId,
        CommandEnvelope<UpdateMediaMixCommand> envelope,
        CancellationToken cancellationToken)
    {
        var mix = await store.FindMixAsync(
            envelope.TenantId, mixVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media mix access denied.");
        var brief = await LoadBudgetReadyBriefAsync(
            mix.BriefVersionId, envelope, cancellationToken);
        var (mediaStrategy, audience) = await RequireCurrentApprovedMediaStrategyAsync(
            envelope.TenantId,
            mix.BriefVersionId,
            brief.Version,
            cancellationToken);
        if (!mix.MediaStrategyArtifactId.HasValue ||
            mix.MediaStrategyArtifactId.Value != mediaStrategy.Id ||
            mix.AudienceArtifactId != audience.Id)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (mix.Status != MasterDataCodes.LifecycleStatuses.Draft)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = envelope.Command.Allocations.Select(ToAllocationView).ToArray();
        EnsureAllocations(allocations, brief.BudgetMinor!.Value);
        EnsureRunningPeriods(allocations);
        await EnsurePurchasesAsync(envelope.TenantId, allocations, cancellationToken);
        var campaignMode = await RequireCampaignModeAsync(
            envelope.TenantId, mix.BriefVersionId, cancellationToken);
        campaignModePolicy.EnsureAllocations(campaignMode.Mode, allocations);
        var allocationsJson = Write(allocations);
        var rolesJson = Write(allocations.ToDictionary(item => item.Channel, item => item.Role));
        var inputHash = IntelligenceInputHash.Combine(
            PlanningHash.ForMix(brief, mix.AudienceArtifactId, allocationsJson),
            mediaStrategy.Id.ToString("N"),
            mediaStrategy.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        var brief = await LoadBudgetReadyBriefAsync(
            mix.BriefVersionId, envelope, cancellationToken);
        var (mediaStrategy, audience) = await RequireCurrentApprovedMediaStrategyAsync(
            envelope.TenantId,
            mix.BriefVersionId,
            brief.Version,
            cancellationToken);
        if (!mix.MediaStrategyArtifactId.HasValue ||
            mix.MediaStrategyArtifactId.Value != mediaStrategy.Id ||
            mix.AudienceArtifactId != audience.Id)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = Read<MediaAllocationView[]>(mix.AllocationsJson);
        EnsureAllocations(allocations, brief.BudgetMinor!.Value);
        EnsureRunningPeriods(allocations);
        await EnsurePurchasesAsync(envelope.TenantId, allocations, cancellationToken);
        var campaignMode = await RequireCampaignModeAsync(
            envelope.TenantId, mix.BriefVersionId, cancellationToken);
        campaignModePolicy.EnsureAllocations(campaignMode.Mode, allocations);
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

    private async Task<PlanningBriefRow> LoadIntelligenceReadyBriefAsync<TCommand>(
        Guid briefVersionId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var brief = await store.FindBriefAsync(
            envelope.TenantId, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Commercial intelligence access denied.");
        if (brief.OwnerUserId != envelope.ActorId.Value)
            throw new UnauthorizedAccessException("Commercial intelligence assignment denied.");
        if (brief.Status != MasterDataCodes.LifecycleStatuses.Ready &&
            brief.Status != MasterDataCodes.LifecycleStatuses.Approved)
            throw new InvalidLifecycleTransitionException();
        return brief;
    }

    private async Task<PlanningBriefRow> LoadBudgetReadyBriefAsync<TCommand>(
        Guid briefVersionId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var brief = await LoadIntelligenceReadyBriefAsync(
            briefVersionId, envelope, cancellationToken);
        if (brief.BudgetUnknown || !brief.BudgetMinor.HasValue ||
            string.IsNullOrWhiteSpace(brief.Currency))
            throw new InvalidLifecycleTransitionException();
        return brief;
    }

    private async Task<(IntelligenceArtifactView Strategy, IntelligenceArtifactView Audience)> RequireCurrentApprovedMediaStrategyAsync(
        TenantId tenantId,
        Guid briefVersionId,
        long briefVersion,
        CancellationToken cancellationToken)
    {
        var strategy = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            store.DbContext,
            tenantId,
            "BriefVersion",
            briefVersionId,
            MasterDataCodes.AgentTypes.MediaStrategy,
            cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        var audience = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            store.DbContext,
            tenantId,
            "BriefVersion",
            briefVersionId,
            MasterDataCodes.AgentTypes.AudienceIntelligence,
            cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        if (strategy.SubjectVersion != briefVersion || audience.SubjectVersion != briefVersion)
            throw new InvalidLifecycleTransitionException();

        var dependencies = await IntelligenceArtifactStore.ReadDependenciesAsync(
            store.DbContext, tenantId, strategy.Id, cancellationToken);
        var audienceDependencies = dependencies
            .Where(item => item.PurposeCode == "approved_audience_strategy")
            .ToArray();
        if (audienceDependencies.Length != 1 ||
            audienceDependencies[0].ResourceId != audience.Id ||
            audienceDependencies[0].ResourceVersion != audience.Version)
            throw new InvalidLifecycleTransitionException();

        var location = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            store.DbContext,
            tenantId,
            "BriefVersion",
            briefVersionId,
            MasterDataCodes.AgentTypes.LocationIntelligence,
            cancellationToken);
        var locationDependencies = dependencies
            .Where(item => item.PurposeCode == "approved_location_intelligence")
            .ToArray();
        if (location is null)
        {
            if (locationDependencies.Length != 0)
                throw new InvalidLifecycleTransitionException();
        }
        else if (location.SubjectVersion != briefVersion ||
                 locationDependencies.Length != 1 ||
                 locationDependencies[0].ResourceId != location.Id ||
                 locationDependencies[0].ResourceVersion != location.Version)
        {
            throw new InvalidLifecycleTransitionException();
        }

        return (strategy, audience);
    }

    private static MediaAllocationView[] BuildWorksheetAllocations(
        MediaStrategyIntelligenceArtifact strategy,
        long budgetMinor)
    {
        if (strategy.ChannelRecommendations.Count == 0 ||
            strategy.ChannelRecommendations.Any(item => !item.BudgetGuidancePercent.HasValue) ||
            strategy.ChannelRecommendations.Sum(item => item.BudgetGuidancePercent!.Value) != 100m)
        {
            throw new InvalidLifecycleTransitionException();
        }

        var allocations = new List<MediaAllocationView>(strategy.ChannelRecommendations.Count);
        long allocated = 0;
        for (var index = 0; index < strategy.ChannelRecommendations.Count; index++)
        {
            var item = strategy.ChannelRecommendations[index];
            var amount = index == strategy.ChannelRecommendations.Count - 1
                ? budgetMinor - allocated
                : decimal.ToInt64(decimal.Truncate(
                    budgetMinor * item.BudgetGuidancePercent!.Value / 100m));
            allocated = checked(allocated + amount);
            allocations.Add(new MediaAllocationView(
                item.Channel,
                amount,
                OpportunityCommandSupport.Required(item.Role, 500, nameof(item.Role)),
                []));
        }
        return allocations.ToArray();
    }

    private static CommercialProblemInput BuildCommercialProblemInput<TCommand>(
        PlanningBriefRow brief,
        CommandEnvelope<TCommand> envelope)
        where TCommand : notnull => new(
            envelope.TenantId.Value, envelope.ActorId.Value,
            envelope.CommandId.Value, envelope.CorrelationId.Value,
            brief.Id, brief.Version, brief.ClientName, brief.BusinessProblem, brief.Objective,
            Read<string[]>(brief.AudiencesJson), Read<string[]>(brief.GeographiesJson),
            Read<string[]>(brief.MediaRequirementsJson), Read<string[]>(brief.ConstraintsJson),
            ReadBrief<CommercialProblemConflictInput[]>(brief.ConflictsJson),
            Read<string[]>(brief.MeasurementJson), brief.BudgetMinor, brief.Currency,
            Read<Guid[]>(brief.EvidenceIdsJson));

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
                new MediaRunningPeriodView(period.Start, period.End)).ToArray(), allocation.Purchases);
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
