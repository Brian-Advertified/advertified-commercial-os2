using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private async Task<AutomationUnderstandingResult> EnsureUnderstandingAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        ActorId owner,
        CancellationToken cancellationToken)
    {
        SuppliedBriefUnderstandingView understanding;
        if (!string.IsNullOrWhiteSpace(run.UnderstandingJson))
        {
            understanding = EmailAutomationRecordStore.Read<SuppliedBriefUnderstandingView>(
                run.UnderstandingJson);
        }
        else
        {
            var clarifications = EmailAutomationRecordStore
                .Read<EmailAutomationClarificationInput[]>(run.ClarificationsJson)
                .Select(item => new BriefClarificationInput(
                    item.FieldPath,
                    item.Value))
                .ToArray();
            understanding = await briefUnderstanding.UnderstandAsync(
                owner,
                new TenantId(context.TenantId),
                new UnderstandSuppliedBriefRequest(
                    context.Subject.Length == 0 ? policy.ProposalTitle : context.Subject,
                    context.BodyText,
                    clarifications),
                cancellationToken);
            understanding = ApplyConfiguredClient(understanding, context.DefaultClientAccountId);
            run = await store.UpdateRunAsync(
                new TenantId(context.TenantId),
                owner,
                context.InboundEmailId,
                current => current with
                {
                    UnderstandingJson = EmailAutomationRecordStore.Write(understanding),
                    IncrementalAiCostMinor = checked(
                        current.IncrementalAiCostMinor + understanding.Usage.IncrementalCostMinor),
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
                cancellationToken);
        }

        understanding = ApplyConfiguredClient(understanding, context.DefaultClientAccountId);
        ValidateUnderstanding(understanding, context.DefaultClientAccountId);
        return new AutomationUnderstandingResult(run, understanding);
    }

    private async Task<EmailAutomationRunRow> EnsureBriefAndStpAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        SuppliedBriefUnderstandingView understanding,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(context.TenantId);
        if (!run.BriefId.HasValue)
        {
            var createCommand = new CreateBriefCommand(
                understanding.Title,
                owner.Value,
                $"inbound-email:{context.InboundEmailId:N}",
                context.Subject,
                context.BodyText,
                context.DefaultClientAccountId,
                context.DefaultClientAccountId.HasValue ? null : understanding.ClientName,
                MasterDataCodes.BriefSourceTypes.InboundEmail);
            var created = await briefCommands.CreateAsync(
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.BriefCreate,
                    0, createCommand, correlationId),
                cancellationToken);
            run = await store.UpdateRunAsync(
                tenantId,
                owner,
                context.InboundEmailId,
                current => current with
                {
                    ClientAccountId = created.Data.ClientId,
                    BriefId = created.Data.Id,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
                cancellationToken);
        }

        if (!run.BriefVersionId.HasValue)
        {
            var versionCommand = ToVersionCommand(run.BriefId!.Value, understanding.Draft);
            var created = await briefCommands.CreateVersionAsync(
                run.BriefId.Value,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.BriefVersionCreate,
                    0, versionCommand, correlationId),
                cancellationToken);
            run = await store.UpdateRunAsync(
                tenantId,
                owner,
                context.InboundEmailId,
                current => current with
                {
                    BriefVersionId = created.Data.Id,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
                cancellationToken);
        }

        var brief = await briefReader.GetAsync(
            owner, tenantId, run.BriefId!.Value, cancellationToken);
        var version = brief.Versions.Single(item => item.Id == run.BriefVersionId);
        if (version.Status == MasterDataCodes.LifecycleStatuses.Draft)
        {
            var submitted = await briefCommands.SubmitAsync(
                version.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.BriefSubmit,
                    version.Version, new SubmitBriefVersionCommand(null, null), correlationId),
                cancellationToken);
            version = submitted.Data;
        }
        if (version.Status == MasterDataCodes.LifecycleStatuses.InReview)
        {
            var approved = await briefCommands.ApproveAsync(
                version.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.BriefApprove,
                    version.Version,
                    new ApproveBriefVersionCommand(
                        "The configured mailbox permits automatic processing of a complete Brief."),
                    correlationId),
                cancellationToken);
            version = approved.Data;
        }
        if (version.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "The campaign Brief is not ready for automatic planning.");
        }

        var workspace = await planningReader.GetWorkspaceAsync(
            owner, tenantId, version.Id, cancellationToken);
        if (workspace.CampaignMode is null)
        {
            await planningCommands.SelectCampaignModeAsync(
                version.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.CampaignModeSelect,
                    0,
                    new SelectCampaignModeCommand(
                        policy.CampaignMode,
                        MasterDataCodes.CampaignModeDecisionSources.InboundAutomation,
                        understanding.CampaignModeConfidence,
                        understanding.CampaignModeRationale),
                    correlationId),
                cancellationToken);
        }
        else if (workspace.CampaignMode.Mode != policy.CampaignMode)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.NonOohRequest,
                "This automation run is not an OOH-only campaign.");
        }

        run = await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.BriefApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        workspace = await planningReader.GetWorkspaceAsync(
            owner, tenantId, version.Id, cancellationToken);
        var stp = workspace.Audience;
        if (stp is null)
        {
            stp = (await planningCommands.GenerateAudiencesAsync(
                version.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.StpGenerate,
                    0, new GenerateAudiencesCommand(), correlationId),
                cancellationToken)).Data;
        }
        var readiness = stpReadiness.Evaluate(stp, policy.MinimumStpConfidence);
        if (!readiness.IsReady)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.StpUnready,
                "The audience strategy needs clarification before automatic planning can continue.");
        }

        return await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                StpVersionId = stp.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.StpApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);
    }

    private static CreateBriefVersionCommand ToVersionCommand(
        Guid briefId,
        SuppliedBriefDraftView draft) => new(
            briefId,
            null,
            draft.BusinessProblem,
            draft.Objective,
            draft.Audiences,
            draft.Geographies,
            draft.Timing,
            draft.BudgetMinor,
            draft.BudgetUnknown,
            draft.Currency,
            draft.VatStatus,
            draft.FeesMinor,
            draft.Constraints,
            draft.Measurement,
            draft.Facts,
            draft.Unknowns,
            draft.Assumptions,
            draft.Conflicts,
            Array.Empty<Guid>());

    private static SuppliedBriefUnderstandingView ApplyConfiguredClient(
        SuppliedBriefUnderstandingView understanding,
        Guid? defaultClientAccountId)
    {
        if (!defaultClientAccountId.HasValue)
        {
            return understanding;
        }
        var questions = understanding.Questions
            .Where(item => !string.Equals(
                item.FieldPath, "clientName", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var unknowns = understanding.Draft.Unknowns
            .Where(item => !string.Equals(
                item.FieldPath, "clientName", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return understanding with
        {
            RequiresHumanClarification = questions.Any(item => item.IsBlocking),
            Questions = questions,
            Draft = understanding.Draft with { Unknowns = unknowns },
        };
    }

    private static void ValidateUnderstanding(
        SuppliedBriefUnderstandingView result,
        Guid? defaultClientAccountId)
    {
        if (result.CampaignMode != MasterDataCodes.CampaignModes.OohOnly)
        {
            throw new EmailAutomationReviewRequiredException(
                result.CampaignMode == MasterDataCodes.CampaignModes.FullCampaign
                    ? MasterDataCodes.AutomationFailureReasons.NonOohRequest
                    : MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                result.CampaignMode == MasterDataCodes.CampaignModes.FullCampaign
                    ? "This request includes media beyond OOH. Start a new full campaign instead."
                    : "The request does not clearly establish an OOH-only campaign.");
        }
        if (result.RequiresHumanClarification ||
            !defaultClientAccountId.HasValue && string.IsNullOrWhiteSpace(result.ClientName) ||
            result.Draft.BudgetUnknown ||
            result.Draft.BudgetMinor is null ||
            string.IsNullOrWhiteSpace(result.Draft.Currency) ||
            string.IsNullOrWhiteSpace(result.Draft.VatStatus) ||
            result.Draft.Audiences.Count == 0 ||
            result.Draft.Geographies.Count == 0 ||
            string.IsNullOrWhiteSpace(result.Draft.Objective) ||
            string.IsNullOrWhiteSpace(result.Draft.Timing))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "The email needs a clear client, objective, audience, geography, dates, budget and VAT status before a proposal can be sent.");
        }
        _ = CampaignTimingParser.Parse(result.Draft.Timing);
    }
}

internal sealed record AutomationUnderstandingResult(
    EmailAutomationRunRow Run,
    SuppliedBriefUnderstandingView Understanding);
