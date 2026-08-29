using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityWorkflowCommands
{
    private async Task EnsureRunPrerequisitesAsync(
        TenantId tenantId,
        Guid opportunityId,
        string runKind,
        CancellationToken cancellationToken)
    {
        var evidenceCount = await store.DbContext.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value"
            FROM commercial.evidence_sets
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            """).SingleAsync(cancellationToken);
        if (evidenceCount == 0)
        {
            throw new EvidenceRequiredException();
        }
        if (runKind == MasterDataCodes.AgentRunKinds.Brief)
        {
            await EnsureApprovedStrategyWithoutBriefAsync(
                tenantId, opportunityId, cancellationToken);
            return;
        }
        if (runKind == MasterDataCodes.AgentRunKinds.Interpretation)
        {
            return;
        }

        var interpretation = await store.FindLatestInterpretationAsync(
            tenantId, opportunityId, cancellationToken);
        if (interpretation?.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new ApprovalRequiredException();
        }
        if (runKind == MasterDataCodes.AgentRunKinds.Angles)
        {
            return;
        }

        var selected = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.opportunity_angles angle
                JOIN commercial.opportunity_angle_sets angle_set
                  ON angle_set.tenant_id = angle.tenant_id AND angle_set.id = angle.angle_set_id
                WHERE angle.tenant_id = {tenantId.Value}
                  AND angle_set.opportunity_id = {opportunityId}
                  AND angle.status_code = {MasterDataCodes.OpportunityAngleStatuses.Selected}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!selected)
        {
            throw new ApprovalRequiredException();
        }
    }

    private async Task EnsureApprovedStrategyWithoutBriefAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        var eligible = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.strategy_versions strategy
                WHERE strategy.tenant_id = {tenantId.Value}
                  AND strategy.opportunity_id = {opportunityId}
                  AND strategy.status_code = {MasterDataCodes.LifecycleStatuses.Approved})
              AND NOT EXISTS (
                SELECT 1 FROM commercial.campaign_briefs brief
                WHERE brief.tenant_id = {tenantId.Value}
                  AND brief.opportunity_id = {opportunityId}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!eligible)
        {
            throw new ApprovalRequiredException();
        }
    }

    private Task<Guid> OpportunityIdForAngleAsync(
        TenantId tenantId,
        Guid angleId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT angle_set.opportunity_id AS "Value"
            FROM commercial.opportunity_angles angle
            JOIN commercial.opportunity_angle_sets angle_set
              ON angle_set.tenant_id = angle.tenant_id AND angle_set.id = angle.angle_set_id
            WHERE angle.tenant_id = {tenantId.Value} AND angle.id = {angleId}
            """).SingleAsync(cancellationToken);
}
