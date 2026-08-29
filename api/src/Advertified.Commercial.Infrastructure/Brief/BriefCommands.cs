using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class BriefCommands(
    BriefRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IBriefCommands
{
    private static readonly string[] ClientAdminRoles =
        [Gate4ReviewerRoles.PlatformAdmin, Gate4ReviewerRoles.AgencyAdmin];

    public async Task<CommandResult<CampaignBriefSummaryView>> CreateAsync(
        CommandEnvelope<CreateBriefCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate5Permissions.BriefCreate,
            token => CreateOutcomeAsync(envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignBriefSummaryView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> CreateVersionAsync(
        Guid briefId,
        CommandEnvelope<CreateBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate5Permissions.BriefEdit,
            token => CreateVersionOutcomeAsync(briefId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> SubmitAsync(
        Guid versionId,
        CommandEnvelope<SubmitBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate5Permissions.BriefSubmit,
            token => SubmitOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> ApproveAsync(
        Guid versionId,
        CommandEnvelope<ApproveBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate5Permissions.BriefApprove,
            token => ApproveOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> RejectAsync(
        Guid versionId,
        CommandEnvelope<RejectBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate5Permissions.BriefApprove,
            token => RejectOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    private async Task<CommandOutcome> CreateOutcomeAsync(
        CommandEnvelope<CreateBriefCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        if (command.OwnerUserId != envelope.ActorId.Value ||
            !await CanCreateForClientAsync(
                envelope.TenantId, command.ClientId, envelope.ActorId.Value, cancellationToken))
        {
            throw new UnauthorizedAccessException("Brief assignment denied.");
        }
        var title = OpportunityCommandSupport.Required(command.Title, 300, nameof(command.Title));
        var sourceTitle = OpportunityCommandSupport.Required(
            command.SourceTitle, 300, nameof(command.SourceTitle));
        var locator = OpportunityCommandSupport.Required(
            command.SourceLocator, 2048, nameof(command.SourceLocator));
        var content = OpportunityCommandSupport.Required(
            command.SourceContent, 262_144, nameof(command.SourceContent));
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, title, owner_user_id, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {id}, {envelope.TenantId.Value}, {command.ClientId}, {title},
                {command.OwnerUserId}, {Gate4Statuses.Created}, 1, {now}, {now});
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title, content,
                content_hash, created_by, created_at_utc)
            VALUES (
                {sourceId}, {envelope.TenantId.Value}, {id}, {Gate5BriefSourceTypes.SuppliedText},
                {locator}, {sourceTitle}, {content}, {OpportunityCommandSupport.Hash(content)},
                {envelope.ActorId.Value}, {now});
            """, cancellationToken);
        var view = new CampaignBriefSummaryView(
            id, envelope.TenantId.Value, command.ClientId, null, title, command.OwnerUserId,
            Gate4Statuses.Created, null, null, 1, now);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1, CommercialResourceTypes.CampaignBrief,
            CommercialActions.BriefCreated, CommercialEventTypes.CampaignBriefCreated, now);
    }

    private Task<bool> CanCreateForClientAsync(
        TenantId tenantId,
        Guid clientId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.client_accounts client
                WHERE client.tenant_id = {tenantId.Value} AND client.id = {clientId}
                  AND (EXISTS (
                        SELECT 1 FROM commercial.memberships membership
                        WHERE membership.tenant_id = client.tenant_id
                          AND membership.user_id = {actorId}
                          AND membership.status_code = {Gate4Statuses.Active}
                          AND membership.role_code = ANY({ClientAdminRoles}))
                    OR EXISTS (
                        SELECT 1 FROM commercial.client_account_assignments assignment
                        WHERE assignment.tenant_id = client.tenant_id
                          AND assignment.client_account_id = client.id
                          AND assignment.user_id = {actorId}
                          AND assignment.effective_from_utc <= now()
                          AND (assignment.effective_to_utc IS NULL
                            OR assignment.effective_to_utc > now())))) AS "Value"
            """).SingleAsync(cancellationToken);
}
