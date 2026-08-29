using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class OpportunityRunStore(
    GovernanceDbContext dbContext,
    OpportunityRecordStore records,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<RunClaim?> ClaimNextAsync(
        Guid workerId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(dbContext, null, null, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var claim = await dbContext.Database.SqlQuery<RunClaim>($"""
            SELECT tenant_id AS "TenantId", run_id AS "RunId", requested_by AS "RequestedBy"
            FROM commercial.claim_next_agent_run(
                {workerId}, {now}, {now.Add(LeaseDuration)})
            """).SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    internal Task<RunWorkRow?> FindWorkAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<RunWorkRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", opportunity_id AS "OpportunityId",
                run_kind_code AS "RunKind", status_code AS "Status",
                input_version AS "InputVersion", requested_by AS "RequestedBy",
                approver_user_id AS "ApproverUserId", correlation_id AS "CorrelationId",
                attempts AS "Attempts", version AS "Version"
            FROM commercial.agent_runs
            WHERE tenant_id = {tenantId.Value} AND id = {runId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<ApprovedEvidenceRow>> ListApprovedEvidenceAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ApprovedEvidenceRow>($"""
            SELECT evidence_set.id AS "EvidenceSetId",
                evidence_set.version_no AS "EvidenceSetVersion", item.id AS "Id",
                item.claim_type_code AS "ClaimType",
                COALESCE(item.reviewed_value_json, item.original_value_json)::text
                    AS "StructuredValueJson",
                item.excerpt AS "Excerpt"
            FROM commercial.evidence_sets evidence_set
            JOIN commercial.evidence_set_items link
              ON link.tenant_id = evidence_set.tenant_id
             AND link.evidence_set_id = evidence_set.id
            JOIN commercial.evidence_items item
              ON item.tenant_id = link.tenant_id AND item.id = link.evidence_item_id
            WHERE evidence_set.tenant_id = {tenantId.Value}
              AND evidence_set.opportunity_id = {opportunityId}
              AND evidence_set.status_code = {Gate4Statuses.Approved}
              AND evidence_set.version_no = (
                SELECT max(candidate.version_no) FROM commercial.evidence_sets candidate
                WHERE candidate.tenant_id = evidence_set.tenant_id
                  AND candidate.opportunity_id = evidence_set.opportunity_id
                  AND candidate.status_code = {Gate4Statuses.Approved})
            ORDER BY item.id
            """).ToListAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        records.BeginSessionAsync(actorId, tenantId, cancellationToken);
}

public sealed record RunClaim
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public Guid RequestedBy { get; set; }
}
