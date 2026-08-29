using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalRecordStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal Task<ApprovedBriefReferenceRow?> FindApprovedBriefAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ApprovedBriefReferenceRow>($"""
            SELECT brief.id AS "BriefId", version.id AS "BriefVersionId",
                version.objective AS "Objective", brief.owner_user_id AS "OwnerUserId",
                version.version AS "BriefVersion"
            FROM commercial.campaign_briefs brief
            JOIN commercial.brief_versions version
              ON version.tenant_id = brief.tenant_id AND version.id = brief.approved_version_id
            WHERE brief.tenant_id = {tenantId.Value} AND brief.id = {briefId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<ProposalRow?> FindProposalAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalRow>($"""
            SELECT id AS "Id", brief_id AS "BriefId", brief_version_id AS "BriefVersionId",
                version_no AS "VersionNumber", title AS "Title",
                executive_summary AS "ExecutiveSummary", terms AS "Terms",
                expiry_at_utc AS "ExpiryAtUtc", status_code AS "Status",
                input_hash AS "InputHash", created_by AS "CreatedBy",
                approved_by AS "ApprovedBy", recipient_user_id AS "RecipientUserId",
                version AS "Version", created_at_utc AS "CreatedAtUtc"
            FROM commercial.proposal_versions
            WHERE tenant_id = {tenantId.Value} AND id = {proposalVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<int> NextVersionNumberAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<int>($"""
            SELECT (COALESCE(MAX(version_no), 0) + 1)::integer AS "Value"
            FROM commercial.proposal_versions
            WHERE tenant_id = {tenantId.Value} AND brief_id = {briefId}
            """).SingleAsync(cancellationToken);

    internal Task<List<ProposalOptionRow>> ListOptionsAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalOptionRow>($"""
            SELECT id AS "Id", plan_version_id AS "PlanVersionId",
                plan_version_no AS "PlanVersionNumber", label AS "Label", outcome AS "Outcome",
                budget_minor AS "BudgetMinor", currency_code AS "Currency",
                display_order AS "DisplayOrder", plan_signature AS "PlanSignature",
                channels_json::text AS "ChannelsJson",
                running_periods_json::text AS "RunningPeriodsJson",
                inventory_json::text AS "InventoryJson"
            FROM commercial.proposal_options
            WHERE tenant_id = {tenantId.Value} AND proposal_version_id = {proposalVersionId}
            ORDER BY display_order
            """).ToListAsync(cancellationToken);

    internal Task<ProposalDocumentRow?> FindDocumentAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalDocumentRow>($"""
            SELECT id AS "Id", proposal_version_id AS "ProposalVersionId",
                media_type AS "MediaType", file_name AS "FileName",
                content_hash AS "ContentHash", content AS "Content",
                created_at_utc AS "CreatedAtUtc"
            FROM commercial.proposal_documents
            WHERE tenant_id = {tenantId.Value} AND proposal_version_id = {proposalVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<ProposalDocumentRow?> FindDocumentByIdAsync(
        TenantId tenantId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalDocumentRow>($"""
            SELECT id AS "Id", proposal_version_id AS "ProposalVersionId",
                media_type AS "MediaType", file_name AS "FileName",
                content_hash AS "ContentHash", content AS "Content",
                created_at_utc AS "CreatedAtUtc"
            FROM commercial.proposal_documents
            WHERE tenant_id = {tenantId.Value} AND id = {documentId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<ProposalDecisionRow?> FindDecisionAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalDecisionRow>($"""
            SELECT decision_code AS "Decision", option_id AS "OptionId", reason AS "Reason",
                decided_by AS "DecidedBy", decided_at_utc AS "DecidedAtUtc"
            FROM commercial.proposal_decisions
            WHERE tenant_id = {tenantId.Value} AND proposal_version_id = {proposalVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<ProposalRecipientRow?> FindRecipientAsync(
        TenantId tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalRecipientRow>($"""
            SELECT users.id AS "UserId", users.display_name AS "DisplayName",
                users.email AS "Email", membership.role_code AS "Role",
                membership.status_code AS "Status"
            FROM commercial.memberships membership
            JOIN commercial.users users ON users.id = membership.user_id
            WHERE membership.tenant_id = {tenantId.Value}
              AND membership.user_id = {userId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<ProposalRecipientRow>> ListRecipientsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalRecipientRow>($"""
            SELECT users.id AS "UserId", users.display_name AS "DisplayName",
                users.email AS "Email", membership.role_code AS "Role",
                membership.status_code AS "Status"
            FROM commercial.memberships membership
            JOIN commercial.users users ON users.id = membership.user_id
            WHERE membership.tenant_id = {tenantId.Value}
              AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND membership.role_code IN (
                  {MasterDataCodes.Roles.AdvertiserAdmin},
                  {MasterDataCodes.Roles.AdvertiserApprover})
            ORDER BY users.display_name, users.id
            """).ToListAsync(cancellationToken);
}
