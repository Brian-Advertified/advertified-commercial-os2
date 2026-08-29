using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityCommands(
    OpportunityRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IOpportunityCommands
{
    private static readonly string[] ClientAdminRoles =
        [Gate4ReviewerRoles.PlatformAdmin, Gate4ReviewerRoles.AgencyAdmin];

    public async Task<CommandResult<OpportunityView>> CreateAsync(
        CommandEnvelope<CreateOpportunityCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.OpportunityCreate,
            token => CreateOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<OpportunityView>(receipt);
    }

    public async Task<CommandResult<EvidenceSourceView>> RegisterEvidenceSourceAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.EvidenceCreate,
            token => RegisterSourceOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<EvidenceSourceView>(receipt);
    }

    public async Task<CommandResult<OpportunityView>> StartQualificationAsync(
        Guid opportunityId,
        CommandEnvelope<StartQualificationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.OpportunityEdit,
            token => StartQualificationOutcomeAsync(opportunityId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<OpportunityView>(receipt);
    }

    private async Task<CommandOutcome> CreateOutcomeAsync(
        CommandEnvelope<CreateOpportunityCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        if (command.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Opportunity assignment denied.");
        }

        var allowed = await CanCreateForClientAsync(
            envelope.TenantId,
            command.ClientId,
            envelope.ActorId.Value,
            cancellationToken);
        if (!allowed)
        {
            throw new UnauthorizedAccessException("Client assignment denied.");
        }

        var title = OpportunityCommandSupport.Required(command.Title, 200, nameof(command.Title));
        var sourceType = OpportunityCommandSupport.Required(
            command.SourceType,
            100,
            nameof(command.SourceType)).ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext,
            "opportunitySourceTypes",
            sourceType,
            cancellationToken);
        var currency = command.Currency?.Trim().ToUpperInvariant();
        if (command.ExpectedValueMinor is < 0 ||
            (command.ExpectedValueMinor.HasValue != !string.IsNullOrWhiteSpace(currency)))
        {
            throw new ArgumentException("Expected value requires non-negative money and currency.");
        }
        if (currency is not null)
        {
            await OpportunityCommandSupport.EnsureCodeAsync(
                store.DbContext,
                "currencies",
                currency,
                cancellationToken);
        }

        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.opportunities (
                id, tenant_id, client_account_id, title, source_type_code, source_ref,
                owner_user_id, stage_code, expected_value_minor, currency_code, deadline,
                problem_summary, objective_summary, version, created_at_utc, updated_at_utc)
            VALUES (
                {id}, {envelope.TenantId.Value}, {command.ClientId}, {title}, {sourceType},
                {OpportunityCommandSupport.Optional(command.SourceRef, 2048, nameof(command.SourceRef))},
                {command.OwnerUserId}, {Gate4Statuses.Created}, {command.ExpectedValueMinor},
                {currency}, {command.Deadline},
                {OpportunityCommandSupport.Optional(command.ProblemSummary, 2000, nameof(command.ProblemSummary))},
                {OpportunityCommandSupport.Optional(command.ObjectiveSummary, 2000, nameof(command.ObjectiveSummary))},
                1, {now}, {now})
            """, cancellationToken);
        var view = new OpportunityView(
            id, envelope.TenantId.Value, command.ClientId, title, sourceType,
            command.SourceRef, command.OwnerUserId, Gate4Statuses.Created,
            command.ExpectedValueMinor, currency, command.Deadline, command.ProblemSummary,
            command.ObjectiveSummary, 1, now);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1, CommercialResourceTypes.Opportunity,
            CommercialActions.OpportunityCreated, CommercialEventTypes.OpportunityCreated, now);
    }

    private async Task<CommandOutcome> RegisterSourceOutcomeAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        await EnsureOwnerAsync(envelope, command.OpportunityId, cancellationToken);
        await OpportunityCommandSupport.EnsureDifferentActiveReviewerAsync(
            store.DbContext,
            envelope.TenantId,
            envelope.ActorId.Value,
            command.ReviewerUserId,
            Gate4ReviewerRoles.Evidence.ToArray(),
            cancellationToken);
        var captured = OpportunityCommandSupport.Capture(command);
        var hash = OpportunityCommandSupport.Hash(captured.Content);
        var existing = await store.FindSourceByHashAsync(
            envelope.TenantId,
            captured.Type,
            hash,
            command.OpportunityId,
            cancellationToken);
        var sourceId = existing?.Id ?? Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        if (existing is null)
        {
            await InsertSourceAsync(envelope, command, captured, sourceId, hash, now, cancellationToken);
        }

        var linked = await LinkSourceAsync(envelope, command.OpportunityId, sourceId, now, cancellationToken);
        if (linked)
        {
            await InsertClaimsAsync(envelope, command, captured, sourceId, now, cancellationToken);
        }
        var view = existing ?? await store.FindSourceByHashAsync(
            envelope.TenantId, captured.Type, hash, command.OpportunityId, cancellationToken)
            ?? throw new InvalidOperationException("The evidence source was not retained.");
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), sourceId, 1, CommercialResourceTypes.EvidenceSource,
            CommercialActions.EvidenceRegistered, CommercialEventTypes.EvidenceRegistered, now);
    }

    private async Task<CommandOutcome> StartQualificationOutcomeAsync(
        Guid opportunityId,
        CommandEnvelope<StartQualificationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var opportunity = await EnsureOwnerAsync(envelope, opportunityId, cancellationToken);
        if (opportunity.Stage != Gate4Statuses.Created)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var hasSource = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.opportunity_evidence_sources
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND opportunity_id = {opportunityId}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!hasSource)
        {
            throw new EvidenceRequiredException();
        }

        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunities
            SET stage_code = {Gate4Statuses.Qualifying}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {opportunityId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var view = opportunity with
        {
            Stage = Gate4Statuses.Qualifying,
            Version = opportunity.Version + 1,
            UpdatedAtUtc = now,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view, opportunityId, view.Version, CommercialResourceTypes.Opportunity,
            CommercialActions.OpportunityQualificationStarted,
            CommercialEventTypes.OpportunityQualificationStarted, now);
    }

    private async Task<OpportunityRow> EnsureOwnerAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Guid opportunityId,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var opportunity = await store.FindOpportunityAsync(
            envelope.TenantId, opportunityId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Opportunity access denied.");
        if (opportunity.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Opportunity access denied.");
        }
        return opportunity;
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
                  AND (
                    EXISTS (
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
                          AND (assignment.effective_to_utc IS NULL OR assignment.effective_to_utc > now()))
                  )) AS "Value"
            """).SingleAsync(cancellationToken);
}
