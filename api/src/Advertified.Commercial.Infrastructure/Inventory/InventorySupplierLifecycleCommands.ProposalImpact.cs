using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySupplierLifecycleCommands
{
    private async Task<CommandOutcome> ResolveProposalImpactOutcomeAsync(
        Guid impactId,
        CommandEnvelope<ResolveProposalInventoryImpactCommand> envelope,
        CancellationToken cancellationToken)
    {
        var impact = await store.FindImpactAsync(
            envelope.TenantId, impactId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal impact access denied.");
        if (impact.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if (impact.Status != MasterDataCodes.ProposalInventoryImpactStatuses.Open)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await EnsureReplacementProposalAsync(
            envelope.TenantId, impact,
            envelope.Command.ReplacementProposalVersionId, cancellationToken);

        var resolution = OpportunityCommandSupport.Required(
            envelope.Command.Resolution, 2000, nameof(envelope.Command.Resolution));
        var now = timeProvider.GetUtcNow();
        var resolutionJson = JsonSerializer.Serialize(new
        {
            replacementProposalVersionId = envelope.Command.ReplacementProposalVersionId,
            resolution,
        });
        await MarkImpactResolvedAsync(
            impact, envelope, resolutionJson, now, cancellationToken);
        await CompleteProposalReviewWhenResolvedAsync(
            envelope.TenantId, impact, envelope.ActorId.Value, now,
            cancellationToken);

        var updated = impact with
        {
            Status = MasterDataCodes.ProposalInventoryImpactStatuses.Resolved,
            ResolvedBy = envelope.ActorId.Value,
            ResolvedAtUtc = now,
            Version = impact.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, updated.ToView(), impact.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.ProposalInventoryImpact,
            MasterDataReferences.CommercialActions.ProposalInventoryReviewResolved,
            MasterDataReferences.CommercialEventTypes.ProposalInventoryReviewResolved,
            now);
    }

    private async Task EnsureReplacementProposalAsync(
        TenantId proposalTenantId,
        ProposalInventoryImpactRow impact,
        Guid replacementProposalVersionId,
        CancellationToken cancellationToken)
    {
        if (replacementProposalVersionId == Guid.Empty ||
            replacementProposalVersionId == impact.ProposalVersionId)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var valid = await store.InventoryStore.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.proposal_versions original
                JOIN commercial.proposal_versions replacement
                  ON replacement.tenant_id = original.tenant_id
                 AND replacement.brief_id = original.brief_id
                 AND replacement.id = {replacementProposalVersionId}
                 AND replacement.version_no > original.version_no
                WHERE original.tenant_id = {proposalTenantId.Value}
                  AND original.id = {impact.ProposalVersionId}
                  AND replacement.inventory_review_status_code =
                        {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
                  AND NOT EXISTS (
                      SELECT 1
                      FROM commercial.proposal_options option
                      JOIN commercial.media_plan_lines line
                        ON line.tenant_id = option.tenant_id
                       AND line.plan_version_id = option.plan_version_id
                      JOIN commercial.inventory_product_versions version
                        ON version.tenant_id = line.inventory_tenant_id
                       AND version.id = line.product_version_id
                      WHERE option.tenant_id = replacement.tenant_id
                        AND option.proposal_version_id = replacement.id
                        AND line.inventory_tenant_id = {impact.InventoryTenantId}
                        AND version.inventory_release_id = {impact.OldReleaseId})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM commercial.proposal_inventory_impacts existing
                      WHERE existing.tenant_id = replacement.tenant_id
                        AND existing.proposal_version_id = replacement.id
                        AND existing.status_code =
                            {MasterDataCodes.ProposalInventoryImpactStatuses.Open})
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!valid)
        {
            throw new ProposalInventoryReviewRequiredException();
        }
    }

    private async Task MarkImpactResolvedAsync(
        ProposalInventoryImpactRow impact,
        CommandEnvelope<ResolveProposalInventoryImpactCommand> envelope,
        string resolutionJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.InventoryStore.DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.proposal_inventory_impacts
                SET status_code =
                        {MasterDataCodes.ProposalInventoryImpactStatuses.Resolved},
                    resolution_json = {resolutionJson}::jsonb,
                    resolved_by = {envelope.ActorId.Value},
                    resolved_at_utc = {now}, version = version + 1
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND id = {impact.Id}
                  AND status_code =
                        {MasterDataCodes.ProposalInventoryImpactStatuses.Open}
                  AND version = {envelope.ExpectedVersion}
                """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private async Task CompleteProposalReviewWhenResolvedAsync(
        TenantId proposalTenantId,
        ProposalInventoryImpactRow impact,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var open = await store.InventoryStore.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.proposal_inventory_impacts item
                WHERE item.tenant_id = {proposalTenantId.Value}
                  AND item.proposal_version_id = {impact.ProposalVersionId}
                  AND item.status_code =
                        {MasterDataCodes.ProposalInventoryImpactStatuses.Open}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (open)
        {
            return;
        }
        await store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Resolved},
                version = version + 1
            WHERE tenant_id = {proposalTenantId.Value}
              AND id = {impact.ProposalVersionId}
            """, cancellationToken);
        await store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                completed_by = {actorId}, completed_at_utc = {now},
                completion_json = {"{\"inventoryReviewResolved\":true}"}::jsonb,
                version = version + 1
            WHERE tenant_id = {proposalTenantId.Value}
              AND resource_id = {impact.ProposalVersionId}
              AND task_type_code =
                    {MasterDataCodes.HumanTaskTypes.InventorySupersessionReview}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
            """, cancellationToken);
    }
}
