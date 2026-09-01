using System.Text.Json;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationRecordStore
{
    private const string EmptyMetadata = "{}";

    internal Task<EmailAutomationRunRow> UpdateRunAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        Func<EmailAutomationRunRow, EmailAutomationRunRow> update,
        CancellationToken cancellationToken) =>
        UpdateRunWithTransitionAsync(
            tenantId,
            actorId,
            inboundEmailId,
            update,
            _ => null,
            cancellationToken);

    internal Task<EmailAutomationRunRow> UpdateRunAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        Func<EmailAutomationRunRow, EmailAutomationRunRow> update,
        EmailAutomationTransition? transition,
        CancellationToken cancellationToken) =>
        UpdateRunWithTransitionAsync(
            tenantId,
            actorId,
            inboundEmailId,
            update,
            _ => transition,
            cancellationToken);

    internal async Task<EmailAutomationRunRow> UpdateRunWithTransitionAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        Func<EmailAutomationRunRow, EmailAutomationRunRow> update,
        Func<EmailAutomationRunRow, EmailAutomationTransition?> transitionFor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var current = await FindRunAsync(tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        var proposed = update(current);
        if (proposed == current)
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }
        var desired = proposed with
        {
            Version = current.Version + 1,
        };
        var changed = await PersistRunAsync(
            tenantId, inboundEmailId, current.Version, desired, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var transition = transitionFor(desired);
        if (transition is not null)
        {
            AddTransition(tenantId, actorId, desired, transition);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return desired;
    }

    private Task<int> PersistRunAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        long currentVersion,
        EmailAutomationRunRow desired,
        CancellationToken cancellationToken)
    {
        var failureCollection = desired.FailureCode is null
            ? null
            : MasterDataCodes.AutomationFailureReasons.Collection;
        var deliveryProviderCollection = desired.DeliveryProviderCode is null
            ? null
            : MasterDataCodes.EmailProviders.Collection;
        return DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = {desired.Status}, checkpoint_code = {desired.Checkpoint},
                client_account_id = {desired.ClientAccountId}, brief_id = {desired.BriefId},
                brief_version_id = {desired.BriefVersionId},
                stp_version_id = {desired.StpVersionId},
                media_mix_version_id = {desired.MediaMixVersionId},
                shortlist_version_id = {desired.ShortlistVersionId},
                media_plan_version_id = {desired.MediaPlanVersionId},
                proposal_version_id = {desired.ProposalVersionId},
                document_id = {desired.DocumentId},
                understanding_json = {desired.UnderstandingJson}::jsonb,
                clarifications_json = {desired.ClarificationsJson}::jsonb,
                failure_collection_code = {failureCollection},
                failure_code = {desired.FailureCode},
                failure_message = {desired.FailureMessage},
                delivery_idempotency_key = {desired.DeliveryIdempotencyKey},
                delivery_provider_collection_code = {deliveryProviderCollection},
                delivery_provider_code = {desired.DeliveryProviderCode},
                delivery_provider_id = {desired.DeliveryProviderId},
                delivery_requested_at_utc = {desired.DeliveryRequestedAtUtc},
                delivery_accepted_at_utc = {desired.DeliveryAcceptedAtUtc},
                incremental_ai_cost_minor = {desired.IncrementalAiCostMinor},
                version = version + 1, updated_at_utc = {desired.UpdatedAtUtc}
            WHERE tenant_id = {tenantId.Value} AND inbound_email_id = {inboundEmailId}
              AND version = {currentVersion}
            """, cancellationToken);
    }

    private void AddTransition(
        TenantId tenantId,
        ActorId actorId,
        EmailAutomationRunRow run,
        EmailAutomationTransition transition)
    {
        var resource = new ResourceReference(
            MasterDataReferences.CommercialResourceTypes.EmailProposalAutomationRun,
            run.Id,
            run.Version);
        DbContext.AuditEvents.Add(new AuditEventRow(
            new AuditRecord(
                Guid.NewGuid(),
                tenantId,
                actorId,
                transition.CommandId,
                transition.CorrelationId,
                transition.Action,
                resource,
                run.UpdatedAtUtc),
            EmptyMetadata));
        DbContext.OutboxMessages.Add(new OutboxMessageRow(
            new OutboxMessage(
                Guid.NewGuid(),
                tenantId,
                transition.CommandId,
                transition.CorrelationId,
                transition.EventType,
                resource,
                JsonSerializer.SerializeToElement(ToView(run)),
                run.UpdatedAtUtc)));
    }
}

internal sealed record EmailAutomationTransition(
    CommandId CommandId,
    CorrelationId CorrelationId,
    ActionCode Action,
    EventTypeCode EventType);
