using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed class ProposalReader(
    ProposalRecordStore store,
    PlanningRecordStore planningStore,
    ITenantAuthorizer authorizer) : IProposalReader
{
    public async Task<ProposalVersionView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken)
    {
        await RequireAsync(actorId, tenantId,
            MasterDataReferences.Permissions.ProposalView, cancellationToken);
        var canPrepare = await CanPrepareAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var proposal = await store.FindProposalAsync(
            tenantId, proposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        EnsureVisible(actorId, proposal, canPrepare);
        var view = await store.BuildViewAsync(tenantId, proposal, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<IReadOnlyList<ApprovedPlanChoiceView>> ListApprovedPlansAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken)
    {
        await RequireAsync(actorId, tenantId,
            MasterDataReferences.Permissions.ProposalGenerate, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var brief = await store.FindApprovedBriefAsync(
            tenantId, briefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        if (brief.OwnerUserId != actorId.Value)
        {
            throw new UnauthorizedAccessException("Proposal assignment denied.");
        }
        var rows = await planningStore.ListApprovedPlansAsync(
            tenantId, brief.BriefVersionId, cancellationToken);
        var plans = await planningStore.BuildPlanViewsAsync(
            tenantId, rows, cancellationToken);
        var views = plans.Select(plan => new ApprovedPlanChoiceView(
            plan.Id, plan.BriefVersionId, plan.VersionNumber, plan.TotalMinor, plan.Currency,
            plan.Lines.Select(item => item.Channel).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            plan.Lines.SelectMany(line => line.RunningPeriods.Select(period =>
                    new ProposalRunningPeriodView(line.Channel, period.Start, period.End)))
                .Distinct().OrderBy(item => item.Channel, StringComparer.Ordinal)
                .ThenBy(item => item.Start).ToArray(),
            plan.CreatedAtUtc)).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return views;
    }

    public async Task<IReadOnlyList<ProposalRecipientView>> ListRecipientsAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await RequireAsync(actorId, tenantId,
            MasterDataReferences.Permissions.ProposalShare, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListRecipientsAsync(tenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(item => new ProposalRecipientView(
            item.UserId, item.DisplayName, item.Email, item.Role)).ToArray();
    }

    public async Task<ProposalDocumentContent> GetDocumentAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await RequireAsync(actorId, tenantId,
            MasterDataReferences.Permissions.ProposalView, cancellationToken);
        var canPrepare = await CanPrepareAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var document = await store.FindDocumentByIdAsync(
            tenantId, documentId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal document access denied.");
        var proposal = await store.FindProposalAsync(
            tenantId, document.ProposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal document access denied.");
        EnsureVisible(actorId, proposal, canPrepare);
        await transaction.CommitAsync(cancellationToken);
        return new ProposalDocumentContent(
            document.Id, document.MediaType, document.FileName, document.Content);
    }

    private static void EnsureVisible(
        ActorId actorId,
        ProposalRow proposal,
        bool canPrepare)
    {
        if (canPrepare) return;
        if (proposal.RecipientUserId != actorId.Value || proposal.Status is not
            (MasterDataCodes.LifecycleStatuses.Sent or
             MasterDataCodes.LifecycleStatuses.Selected or
             MasterDataCodes.LifecycleStatuses.Declined))
        {
            throw new UnauthorizedAccessException("Proposal access denied.");
        }
    }

    private async Task<bool> CanPrepareAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        (await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.ProposalGenerate,
            cancellationToken)).IsAllowed;

    private async Task RequireAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Proposal access denied.");
        }
    }
}
