using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> GenerateMediaPlanOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaPlanCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadApprovedBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var mix = await store.FindLatestMixAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var shortlist = await store.FindLatestShortlistAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        if (mix is null || shortlist is null || mix.Status != MasterDataCodes.LifecycleStatuses.Approved ||
            shortlist.Status != MasterDataCodes.LifecycleStatuses.Approved || shortlist.MixVersionId != mix.Id)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var shortlistView = await store.BuildShortlistViewAsync(
            envelope.TenantId, shortlist, cancellationToken);
        var selected = shortlistView.Candidates
            .Where(item => item.IsEligible && item.IsSelected == true).ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var inventory = await store.ListInventoryAsync(envelope.TenantId, cancellationToken);
        var byVersion = inventory.ToDictionary(item => item.ProductVersionId);
        var inputs = selected.Select(item => byVersion[item.ProductVersionId]).ToArray();
        EnsureSelectedInputsCurrent(selected, inputs);
        var allocations = Read<MediaAllocationView[]>(mix.AllocationsJson)
            .ToDictionary(item => item.Channel, StringComparer.Ordinal);
        var scheduled = inputs.Select(item => new ScheduledInventory(
            item,
            allocations.TryGetValue(item.Channel, out var allocation)
                ? allocation.RunningPeriods
                : throw new InvalidLifecycleTransitionException())).ToArray();
        var amounts = PlanAmounts.Calculate(brief, scheduled, planningPolicy);
        if (amounts.TotalMinor > brief.BudgetMinor)
        {
            throw new PlanningApprovalBlockedException();
        }
        var supplyConfidence = PlanSupply.Overall(scheduled, timeProvider.GetUtcNow());
        var objections = CreateObjections(selected, supplyConfidence);
        var latest = await store.FindLatestPlanAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var inputHash = PlanningHash.ForPlan(shortlist, selected, allocations.Values);
        var assumptions = new[]
        {
            "No reach or performance forecast is claimed without supplied evidence.",
            "Supply state is retained from the exact latest published availability record.",
        };
        var forecastJson = Write(new
        {
            source = MasterDataCodes.SupplySourceTypes.PublishedInventory,
            confidence = supplyConfidence,
            reach = (long?)null,
            impressions = (long?)null,
        });
        var assumptionsJson = Write(assumptions);
        var criticJson = Write(objections);
        var now = timeProvider.GetUtcNow();
        await InsertPlanVersionAsync(
            envelope, id, brief, mix, shortlist, versionNumber, amounts, supplyConfidence,
            inputHash, forecastJson, assumptionsJson, criticJson, now, cancellationToken);
        await InsertPlanLinesAsync(
            envelope.TenantId, id, selected, amounts, now, cancellationToken);
        var row = await store.FindPlanAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The media plan was not persisted.");
        var view = await store.BuildPlanViewAsync(envelope.TenantId, row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, row.Version, MasterDataReferences.CommercialResourceTypes.MediaPlanVersion,
            MasterDataReferences.CommercialActions.MediaPlanGenerated, MasterDataReferences.CommercialEventTypes.MediaPlanGenerated, now);
    }

    private async Task<CommandOutcome> ResolvePlanObjectionOutcomeAsync(
        Guid planVersionId,
        string objectionCode,
        CommandEnvelope<ResolvePlanObjectionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanContextAsync(planVersionId, envelope, cancellationToken);
        var code = OpportunityCommandSupport.Required(
            objectionCode, 100, nameof(objectionCode));
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 2000, nameof(envelope.Command.Reason));
        if (envelope.Command.Resolution is not
            (MasterDataCodes.ObjectionResolutions.Addressed or MasterDataCodes.ObjectionResolutions.AcceptedWithReason))
        {
            throw new ArgumentException("The objection resolution is invalid.");
        }
        var objection = Read<CriticObjection[]>(plan.CriticReportJson)
            .SingleOrDefault(item => item.Code == code)
            ?? throw new ArgumentException("The objection does not exist.");
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.planning_objection_resolutions (
                id, tenant_id, plan_version_id, objection_code, severity_code,
                resolution_code, reason, resolved_by, resolved_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {planVersionId}, {code},
                {objection.Severity}, {envelope.Command.Resolution}, {reason},
                {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.media_plan_versions SET version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {planVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.InReview}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = plan with { Version = plan.Version + 1 };
        var view = await store.BuildPlanViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, planVersionId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.MediaPlanVersion,
            MasterDataReferences.CommercialActions.MediaPlanObjectionResolved,
            MasterDataReferences.CommercialEventTypes.MediaPlanObjectionResolved, now);
    }

    private async Task<CommandOutcome> ApproveMediaPlanOutcomeAsync(
        Guid planVersionId,
        CommandEnvelope<ApproveMediaPlanCommand> envelope,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanContextAsync(planVersionId, envelope, cancellationToken);
        var view = await store.BuildPlanViewAsync(
            envelope.TenantId, plan, cancellationToken);
        if (view.Objections.Any(item =>
                item.Severity is MasterDataCodes.CriticSeverities.Critical or MasterDataCodes.CriticSeverities.Material &&
                item.Resolution is null))
        {
            throw new PlanningApprovalBlockedException();
        }
        if (!await InputsRemainCurrentAsync(envelope.TenantId, view, cancellationToken))
        {
            throw new PlanningInputStaleException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.media_plan_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, approved_by = {envelope.ActorId.Value},
                approved_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {planVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.InReview}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = plan with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ApprovedBy = envelope.ActorId.Value,
            Version = plan.Version + 1,
        };
        var approved = await store.BuildPlanViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, approved, planVersionId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.MediaPlanVersion,
            MasterDataReferences.CommercialActions.MediaPlanApproved, MasterDataReferences.CommercialEventTypes.MediaPlanApproved, now);
    }

    private async Task<MediaPlanRow> LoadPlanContextAsync<TCommand>(
        Guid planVersionId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var plan = await store.FindPlanAsync(
            envelope.TenantId, planVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media plan access denied.");
        await LoadApprovedBriefAsync(plan.BriefVersionId, envelope, cancellationToken);
        if (plan.Status != MasterDataCodes.LifecycleStatuses.InReview)
        {
            throw new InvalidLifecycleTransitionException();
        }
        return plan;
    }

    private async Task<bool> InputsRemainCurrentAsync(
        TenantId tenantId,
        MediaPlanVersionView plan,
        CancellationToken cancellationToken)
    {
        var current = await store.ListInventoryAsync(tenantId, cancellationToken);
        var byProduct = current.ToDictionary(item => item.ProductId);
        return plan.Lines.All(line => byProduct.TryGetValue(line.InventoryProductId, out var item) &&
            item.ProductVersionId == line.ProductVersionId && item.RateId == line.RateId &&
            item.AvailabilityId == line.AvailabilityId);
    }

    private static void EnsureSelectedInputsCurrent(
        IReadOnlyList<InventoryShortlistCandidateView> selected,
        IReadOnlyList<PlanningInventoryRow> current)
    {
        if (selected.Zip(current).Any(pair =>
                pair.First.ProductVersionId != pair.Second.ProductVersionId ||
                pair.First.RateId != pair.Second.RateId ||
                pair.First.AvailabilityId != pair.Second.AvailabilityId))
        {
            throw new PlanningInputStaleException();
        }
    }

    private static CriticObjection[] CreateObjections(
        IReadOnlyList<InventoryShortlistCandidateView> selected,
        string supplyConfidence)
    {
        var objections = new List<CriticObjection>();
        if (supplyConfidence != MasterDataCodes.SupplyConfidenceStatuses.Confirmed)
        {
            objections.Add(new CriticObjection(
                MasterDataCodes.PlanningObjectionTypes.SupplyUnconfirmed, MasterDataCodes.CriticSeverities.Material,
                "supply", "At least one selected line has unconfirmed supply.",
                "Confirm supply or explicitly accept the uncertainty."));
        }
        if (selected.Any(item => item.Channel is MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh &&
                item.Benchmark?.Position == MasterDataCodes.BenchmarkPositions.Insufficient))
        {
            objections.Add(new CriticObjection(
                MasterDataCodes.PlanningObjectionTypes.BenchmarkInsufficient, MasterDataCodes.CriticSeverities.Material,
                "benchmark", "A selected OOH/DOOH line has fewer than three local peers.",
                "Review the visible cohort and accept or replace the line."));
        }
        return objections.ToArray();
    }
}
