using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private async Task<EmailAutomationRunRow> EnsurePlanningAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        SuppliedBriefUnderstandingView understanding,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(context.TenantId);
        var briefVersionId = run.BriefVersionId
            ?? throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "The campaign Brief is not available for planning.");
        var workspace = await planningReader.GetWorkspaceAsync(
            owner, tenantId, briefVersionId, cancellationToken);
        if (workspace.CampaignMode?.Mode != policy.CampaignMode)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.NonOohRequest,
                "The campaign is not locked to OOH-only media.");
        }
        if (workspace.Audience is null ||
            !stpReadiness.Evaluate(
                workspace.Audience, policy.MinimumStpConfidence).IsReady)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.StpUnready,
                "The audience strategy is not ready for automatic planning.");
        }

        var mix = workspace.MediaMix;
        if (mix is null)
        {
            mix = (await planningCommands.GenerateMediaMixAsync(
                briefVersionId,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.MixGenerate,
                    0, new GenerateMediaMixCommand(), correlationId),
                cancellationToken)).Data;
        }
        if (mix.Status == MasterDataCodes.LifecycleStatuses.Draft)
        {
            var runningPeriods = CampaignTimingParser.Parse(understanding.Draft.Timing);
            var allocations = mix.Allocations.Select(item => new MediaAllocationInput(
                item.Channel,
                item.BudgetMinor,
                item.Role,
                runningPeriods)).ToArray();
            EnsureAutomaticMix(allocations, understanding.Draft.BudgetMinor!.Value);
            mix = (await planningCommands.UpdateMediaMixAsync(
                mix.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.MixSchedule,
                    mix.Version,
                    new UpdateMediaMixCommand(
                        allocations,
                        "Apply the campaign dates extracted from the complete inbound Brief."),
                    correlationId),
                cancellationToken)).Data;
            mix = (await planningCommands.ApproveMediaMixAsync(
                mix.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.MixApprove,
                    mix.Version,
                    new ApproveMediaMixCommand(
                        "The configured mailbox permits automatic processing of a complete OOH Brief."),
                    correlationId),
                cancellationToken)).Data;
        }
        if (mix.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.PlanUnready,
                "The OOH media mix is not ready for automatic planning.");
        }
        EnsureAutomaticMix(
            mix.Allocations.Select(item => new MediaAllocationInput(
                item.Channel,
                item.BudgetMinor,
                item.Role,
                item.RunningPeriods.Select(period => new MediaRunningPeriodInput(
                    period.Start, period.End)).ToArray())).ToArray(),
            understanding.Draft.BudgetMinor!.Value);
        run = await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                MediaMixVersionId = mix.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.MixApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        workspace = await planningReader.GetWorkspaceAsync(
            owner, tenantId, briefVersionId, cancellationToken);
        var shortlist = workspace.Shortlist;
        if (shortlist is null || shortlist.MixVersionId != mix.Id)
        {
            shortlist = (await planningCommands.GenerateShortlistAsync(
                briefVersionId,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ShortlistGenerate,
                    0, new GenerateShortlistCommand(), correlationId),
                cancellationToken)).Data;
        }
        if (shortlist.Status == MasterDataCodes.LifecycleStatuses.Draft)
        {
            var selectedIds = await inventorySelector.SelectAsync(
                tenantId, owner, mix, shortlist, timeProvider.GetUtcNow(), cancellationToken);
            if (selectedIds.Length == 0)
            {
                throw new EmailAutomationReviewRequiredException(
                    MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                    "No confirmed eligible OOH inventory is available for this request.");
            }
            shortlist = (await planningCommands.SelectShortlistAsync(
                shortlist.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ShortlistSelect,
                    shortlist.Version,
                    new SelectShortlistCommand(
                        selectedIds,
                        "Select the highest-scoring confirmed inventory for automatic planning."),
                    correlationId),
                cancellationToken)).Data;
        }
        if (shortlist.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                "The inventory selection is not ready for automatic planning.");
        }
        run = await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                ShortlistVersionId = shortlist.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.ShortlistApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        workspace = await planningReader.GetWorkspaceAsync(
            owner, tenantId, briefVersionId, cancellationToken);
        var plan = workspace.MediaPlan;
        if (plan is null || plan.ShortlistVersionId != shortlist.Id)
        {
            plan = (await planningCommands.GenerateMediaPlanAsync(
                briefVersionId,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.PlanGenerate,
                    0, new GenerateMediaPlanCommand(), correlationId),
                cancellationToken)).Data;
        }
        EmailAutomationPlanReadiness.EnsureReady(plan);
        if (plan.Status == MasterDataCodes.LifecycleStatuses.InReview)
        {
            plan = (await planningCommands.ApproveMediaPlanAsync(
                plan.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.PlanApprove,
                    plan.Version,
                    new ApproveMediaPlanCommand(
                        "The plan is reconciled and meets the automatic-send readiness policy."),
                    correlationId),
                cancellationToken)).Data;
        }
        if (plan.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.PlanUnready,
                "The media plan is not approved for automatic proposal creation.");
        }
        EmailAutomationPlanReadiness.EnsureReady(plan);
        return await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                MediaPlanVersionId = plan.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.PlanApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);
    }

    private void EnsureAutomaticMix(
        MediaAllocationInput[] allocations,
        long budgetMinor)
    {
        if (allocations.Length == 0 ||
            allocations.Sum(item => item.BudgetMinor) != budgetMinor ||
            allocations.Any(item => item.BudgetMinor <= 0 ||
                item.RunningPeriods.Count == 0 ||
                !policy.AllowedChannels.Contains(item.Channel, StringComparer.Ordinal)))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.PlanUnready,
                "The OOH media mix does not reconcile to the supplied budget and dates.");
        }
    }
}
