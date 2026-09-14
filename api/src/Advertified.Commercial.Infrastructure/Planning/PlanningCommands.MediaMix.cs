using System.Globalization;
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

        var existingAllocations = Read<MediaAllocationView[]>(mix.AllocationsJson)
            .ToDictionary(item => item.Channel, StringComparer.OrdinalIgnoreCase);
        var allocations = envelope.Command.Allocations.Select(item => {
            var allocation = ToAllocationView(item);
            if (item.GeographyAllocations is not null ||
                !existingAllocations.TryGetValue(allocation.Channel, out var existing)) return allocation;
            return allocation with {
                GeographyAllocations = ScaleGeographyAllocations(existing.GeographyAllocations, allocation.BudgetMinor),
            };
        }).ToArray();
        EnsureAllocations(allocations, brief.BudgetMinor!.Value);
        EnsureRunningPeriods(allocations);
        var allowedGeographies = Read<string[]>(brief.GeographiesJson);
        EnsureGeographyAllocations(allocations, allowedGeographies, requireComplete: false);
        var impactEstimate = ToImpactEstimateView(envelope.Command.ImpactEstimate);
        EnsureImpactEstimate(impactEstimate);
        await EnsurePurchasesAsync(envelope.TenantId, allocations, cancellationToken);

        var campaignMode = await RequireCampaignModeAsync(
            envelope.TenantId, mix.BriefVersionId, cancellationToken);
        campaignModePolicy.EnsureAllocations(campaignMode.Mode, allocations);
        var allocationsJson = Write(allocations);
        var rolesJson = Write(allocations.ToDictionary(item => item.Channel, item => item.Role));
        var impactEstimateJson = impactEstimate is null ? null : Write(impactEstimate);
        var inputHash = IntelligenceInputHash.Combine(
            PlanningHash.ForMix(brief, mix.AudienceArtifactId, allocationsJson, impactEstimateJson),
            mediaStrategy.Id.ToString("N"),
            mediaStrategy.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.media_mix_versions
            SET allocations_json = {allocationsJson}::jsonb,
                channel_roles_json = {rolesJson}::jsonb,
                planning_impact_json = {impactEstimateJson}::jsonb,
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
            ImpactEstimateJson = impactEstimateJson,
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
        EnsureGeographyAllocations(
            allocations,
            Read<string[]>(brief.GeographiesJson),
            requireComplete: true);
        var impactEstimate = string.IsNullOrWhiteSpace(mix.ImpactEstimateJson)
            ? null
            : JsonSerializer.Deserialize<MediaImpactEstimateView>(mix.ImpactEstimateJson, StoredJson);
        EnsureImpactEstimate(impactEstimate);
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
        long budgetMinor,
        string timing,
        string[] geographies)
    {
        if (strategy.ChannelRecommendations.Count == 0 ||
            strategy.ChannelRecommendations.Any(item => !item.BudgetGuidancePercent.HasValue) ||
            strategy.ChannelRecommendations.Sum(item => item.BudgetGuidancePercent!.Value) != 100m)
        {
            throw new InvalidLifecycleTransitionException();
        }

        var runningPeriods = TryParsePlanningPeriod(timing, out var start, out var end)
            ? new[] { new MediaRunningPeriodView(start, end) }
            : [];
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
            var geographyAllocations = SplitGeographyBudget(amount, geographies);
            allocations.Add(new MediaAllocationView(
                item.Channel,
                amount,
                OpportunityCommandSupport.Required(item.Role, 500, nameof(item.Role)),
                runningPeriods,
                null,
                geographyAllocations));
        }
        return allocations.ToArray();
    }

    private static MediaGeographyAllocationView[]? SplitGeographyBudget(
        long amount,
        string[] geographies)
    {
        var markets = geographies
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (markets.Length == 0) return null;
        var result = new MediaGeographyAllocationView[markets.Length];
        var baseAmount = amount / markets.Length;
        long allocated = 0;
        for (var index = 0; index < markets.Length; index++)
        {
            var value = index == markets.Length - 1 ? amount - allocated : baseAmount;
            allocated = checked(allocated + value);
            result[index] = new MediaGeographyAllocationView(markets[index], value);
        }
        return result;
    }

    private static bool TryParsePlanningPeriod(
        string timing,
        out DateOnly start,
        out DateOnly end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(timing)) return false;
        var normalized = timing.Trim().TrimEnd('.', ';');
        var parts = normalized.Split(" to ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            if (!TryParsePlanningBoundary(parts[0], firstDay: true, out start) ||
                !TryParsePlanningBoundary(parts[0], firstDay: false, out end))
            {
                return false;
            }
            return true;
        }
        if (parts.Length != 2) return false;
        if (!TryParsePlanningBoundary(parts[0], firstDay: true, out start) ||
            !TryParsePlanningBoundary(parts[1], firstDay: false, out end) ||
            start > end)
        {
            return false;
        }
        return true;
    }

    private static bool TryParsePlanningBoundary(
        string value,
        bool firstDay,
        out DateOnly result)
    {
        var formats = new[] { "d MMMM yyyy", "dd MMMM yyyy", "d MMM yyyy", "dd MMM yyyy", "yyyy-MM-dd" };
        if (DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result))
        {
            return true;
        }
        if (!DateTime.TryParseExact(value.Trim(), "MMMM yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var month))
        {
            result = default;
            return false;
        }
        var day = firstDay ? 1 : DateTime.DaysInMonth(month.Year, month.Month);
        result = new DateOnly(month.Year, month.Month, day);
        return true;
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
        var geographyAllocations = allocation.GeographyAllocations?.Select(item =>
            new MediaGeographyAllocationView(
                OpportunityCommandSupport.Required(item.Geography, 300, nameof(item.Geography)),
                item.BudgetMinor)).ToArray();
        return new MediaAllocationView(
            channel,
            allocation.BudgetMinor,
            role,
            allocation.RunningPeriods.Select(period =>
                new MediaRunningPeriodView(period.Start, period.End)).ToArray(),
            allocation.Purchases,
            geographyAllocations,
            ToScheduleView(allocation.Schedule));
    }

    private static MediaScheduleView? ToScheduleView(MediaScheduleInput? input)
    {
        if (input is null) return null;
        return new MediaScheduleView(
            input.Weekdays.Select(value => OpportunityCommandSupport.Required(value, 20, nameof(input.Weekdays)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            input.Dayparts.Select(value => OpportunityCommandSupport.Required(value, 50, nameof(input.Dayparts)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static MediaImpactEstimateView? ToImpactEstimateView(MediaImpactEstimateInput? input)
    {
        if (input is null) return null;
        return new MediaImpactEstimateView(
            input.EstimatedReach,
            input.AverageFrequency,
            input.EstimatedRoiPercent,
            OpportunityCommandSupport.Required(input.Source, 500, nameof(input.Source)),
            OpportunityCommandSupport.Optional(input.MeasurementPeriod, 200, nameof(input.MeasurementPeriod)),
            OpportunityCommandSupport.Required(input.Methodology, 1000, nameof(input.Methodology)));
    }

    private static IReadOnlyList<MediaGeographyAllocationView>? ScaleGeographyAllocations(
        IReadOnlyList<MediaGeographyAllocationView>? values,
        long nextBudget)
    {
        if (values is null || values.Count == 0) return values;
        if (values.Count == 1) return [values[0] with { BudgetMinor = nextBudget }];
        var sourceTotal = values.Sum(item => item.BudgetMinor);
        if (sourceTotal <= 0) return [];
        var result = new List<MediaGeographyAllocationView>(values.Count);
        long assigned = 0;
        for (var index = 0; index < values.Count; index++)
        {
            var budgetMinor = index == values.Count - 1
                ? nextBudget - assigned
                : decimal.ToInt64(decimal.Round(
                    nextBudget * values[index].BudgetMinor / (decimal)sourceTotal, 0, MidpointRounding.AwayFromZero));
            budgetMinor = Math.Max(0, budgetMinor);
            assigned = checked(assigned + budgetMinor);
            result.Add(values[index] with { BudgetMinor = budgetMinor });
        }
        return result;
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

    private static void EnsureGeographyAllocations(
        IReadOnlyList<MediaAllocationView> allocations,
        IReadOnlyList<string> allowedGeographies,
        bool requireComplete)
    {
        var allowed = allowedGeographies
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var allocation in allocations)
        {
            var geographies = allocation.GeographyAllocations ?? [];
            if (geographies.Count == 0)
            {
                if (requireComplete && allowed.Count > 0)
                    throw new ArgumentException("Allocate every funded media type across the approved campaign geographies before approval.");
                continue;
            }
            if (geographies.Any(item => item.BudgetMinor < 0) ||
                geographies.Select(item => item.Geography).Distinct(StringComparer.OrdinalIgnoreCase).Count() != geographies.Count ||
                geographies.Any(item => !allowed.Contains(item.Geography)) ||
                geographies.Sum(item => item.BudgetMinor) != allocation.BudgetMinor)
            {
                throw new ArgumentException("Geography allocations must use approved campaign markets and reconcile to each media budget.");
            }
        }
    }

    private static void EnsureImpactEstimate(MediaImpactEstimateView? estimate)
    {
        if (estimate is null) return;
        if ((!estimate.EstimatedReach.HasValue && !estimate.AverageFrequency.HasValue && !estimate.EstimatedRoiPercent.HasValue) ||
            estimate.EstimatedReach is <= 0 or > 10_000_000_000m ||
            estimate.AverageFrequency is <= 0 or > 1_000m ||
            estimate.EstimatedRoiPercent is < -100m or > 100_000m)
        {
            throw new ArgumentException("The planning impact estimate is outside the supported bounds.");
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
