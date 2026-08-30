using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class BriefCommands
{
    private async Task<CommandOutcome> CreateVersionOutcomeAsync(
        Guid briefId,
        CommandEnvelope<CreateBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Command.BriefId != briefId)
        {
            throw new ArgumentException("The route and Brief must match.");
        }
        var brief = await store.FindBriefForUpdateAsync(
            envelope.TenantId, briefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        EnsureOwner(brief, envelope.ActorId.Value);
        var sourceId = await ResolveVersionSourceAsync(
            brief, envelope.Command.BaseVersionId, envelope.TenantId, cancellationToken);
        var value = await BriefCommandSupport.ValidateAsync(
            store.DbContext, envelope.Command, cancellationToken);
        var evidenceIds = envelope.Command.EvidenceItemIds.Distinct().ToArray();
        await EnsureEvidenceAsync(brief, evidenceIds, envelope, cancellationToken);
        var versionNumber = await NextVersionAsync(
            envelope.TenantId.Value, briefId, cancellationToken);
        var versionId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var evidenceBindings = evidenceIds.Length == 0
            ? BriefCommandSupport.Json(Array.Empty<object>())
            : BriefCommandSupport.Json(new[]
            {
                new { fieldPath = "brief.materialFields", evidenceItemIds = evidenceIds },
            });
        await BriefPersistence.InsertVersionAsync(
            store.DbContext,
            new BriefVersionWrite(
                versionId, envelope.TenantId, brief.Id, envelope.Command.BaseVersionId,
                sourceId, versionNumber, envelope.Command, value, evidenceBindings,
                MasterDataCodes.LifecycleStatuses.Draft, envelope.ActorId.Value, 1, now),
            cancellationToken);
        await BriefPersistence.BindEvidenceAsync(
            store.DbContext, envelope.TenantId, versionId, evidenceIds, cancellationToken);
        await BriefPersistence.SetCurrentDraftAsync(
            store.DbContext, envelope.TenantId, brief.Id, versionId, brief.Version,
            MasterDataCodes.LifecycleStatuses.Draft, now, cancellationToken);
        var row = await store.FindVersionAsync(
            envelope.TenantId, versionId, cancellationToken)
            ?? throw new InvalidOperationException("The Brief version was not retained.");
        return OpportunityCommandSupport.Outcome(
            envelope, row.ToView(), versionId, 1, MasterDataReferences.CommercialResourceTypes.BriefVersion,
            MasterDataReferences.CommercialActions.BriefVersionCreated, MasterDataReferences.CommercialEventTypes.BriefVersionCreated, now);
    }

    private async Task<Guid> ResolveVersionSourceAsync(
        CampaignBriefRow brief,
        Guid? baseVersionId,
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (baseVersionId.HasValue)
        {
            if (brief.CurrentDraftVersionId != baseVersionId)
            {
                throw new VersionConflictException();
            }
            var baseVersion = await store.FindVersionAsync(
                tenantId, baseVersionId.Value, cancellationToken)
                ?? throw new VersionConflictException();
            if (baseVersion.BriefId != brief.Id)
            {
                throw new VersionConflictException();
            }
            return baseVersion.SourceId;
        }
        if (brief.CurrentDraftVersionId.HasValue)
        {
            throw new VersionConflictException();
        }
        return (await store.FindFirstSourceAsync(tenantId, brief.Id, cancellationToken))?.Id
            ?? throw new InvalidOperationException("A retained Brief source is required.");
    }

    private async Task EnsureEvidenceAsync(
        CampaignBriefRow brief,
        Guid[] evidenceIds,
        CommandEnvelope<CreateBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (evidenceIds.Length == 0)
        {
            return;
        }
        if (!brief.OpportunityId.HasValue)
        {
            throw new EvidenceRequiredException();
        }
        var count = await store.DbContext.Database.SqlQuery<int>($"""
            SELECT count(DISTINCT item.id)::integer AS "Value"
            FROM commercial.evidence_items item
            JOIN commercial.evidence_set_items link
              ON link.tenant_id = item.tenant_id AND link.evidence_item_id = item.id
            JOIN commercial.evidence_sets evidence_set
              ON evidence_set.tenant_id = link.tenant_id
             AND evidence_set.id = link.evidence_set_id
            WHERE item.tenant_id = {envelope.TenantId.Value}
              AND item.id = ANY({evidenceIds})
              AND evidence_set.opportunity_id = {brief.OpportunityId.Value}
              AND evidence_set.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            """).SingleAsync(cancellationToken);
        if (count != evidenceIds.Length)
        {
            throw new EvidenceRequiredException();
        }
    }

    private Task<int> NextVersionAsync(
        Guid tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<int>($"""
            SELECT (COALESCE(max(version_no), 0) + 1)::integer AS "Value"
            FROM commercial.brief_versions
            WHERE tenant_id = {tenantId} AND brief_id = {briefId}
            """).SingleAsync(cancellationToken);

    private static void EnsureOwner(CampaignBriefRow brief, Guid actorId)
    {
        if (brief.OwnerUserId != actorId)
        {
            throw new UnauthorizedAccessException("Brief access denied.");
        }
    }
}
