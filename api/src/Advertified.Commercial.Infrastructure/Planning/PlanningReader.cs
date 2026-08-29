using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class PlanningReader(
    PlanningRecordStore store,
    ITenantAuthorizer authorizer) : IPlanningReader
{
    public async Task<PlanningWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var brief = await store.FindBriefAsync(tenantId, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Planning access denied.");
        EnsureAssigned(brief, actorId);
        var audienceRow = await store.FindLatestAudienceAsync(
            tenantId, briefVersionId, cancellationToken);
        var mixRow = await store.FindLatestMixAsync(tenantId, briefVersionId, cancellationToken);
        var shortlistRow = await store.FindLatestShortlistAsync(
            tenantId, briefVersionId, cancellationToken);
        var planRow = await store.FindLatestPlanAsync(
            tenantId, briefVersionId, cancellationToken);
        var audience = audienceRow is null ? null : await store.BuildAudienceViewAsync(
            tenantId, audienceRow, cancellationToken);
        var mix = mixRow is null ? null : PlanningRecordStore.BuildMixView(mixRow);
        var shortlist = shortlistRow is null ? null : await store.BuildShortlistViewAsync(
            tenantId, shortlistRow, cancellationToken);
        var plan = planRow is null ? null : await store.BuildPlanViewAsync(
            tenantId, planRow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlanningWorkspaceView(brief.BriefId, briefVersionId, audience, mix, shortlist, plan);
    }

    public async Task<MediaPlanVersionView> GetPlanAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid planVersionId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var plan = await store.FindPlanAsync(tenantId, planVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Plan access denied.");
        var brief = await store.FindBriefAsync(
            tenantId, plan.BriefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Plan access denied.");
        EnsureAssigned(brief, actorId);
        var view = await store.BuildPlanViewAsync(tenantId, plan, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.PlanView, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Planning access denied.");
        }
    }

    private static void EnsureAssigned(PlanningBriefRow brief, ActorId actorId)
    {
        if (brief.OwnerUserId != actorId.Value)
        {
            throw new UnauthorizedAccessException("Planning assignment denied.");
        }
    }
}
