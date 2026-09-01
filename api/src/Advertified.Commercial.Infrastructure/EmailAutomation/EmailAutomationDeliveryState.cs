using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationRecordStore
{
    internal async Task<DeliveryIntentResult> BeginDeliveryAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        string providerCode,
        string idempotencyKey,
        DateTimeOffset requestedAtUtc,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var current = await FindRunAsync(tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        if (current.DeliveryRequestedAtUtc.HasValue)
        {
            EnsureIntentMatches(current, providerCode, idempotencyKey);
            await transaction.CommitAsync(cancellationToken);
            return new DeliveryIntentResult(current, false);
        }

        var desired = current with
        {
            Status = MasterDataCodes.EmailAutomationStatuses.Processing,
            Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.DeliveryRequested,
            FailureCode = null,
            FailureMessage = null,
            DeliveryIdempotencyKey = idempotencyKey,
            DeliveryProviderCode = providerCode,
            DeliveryRequestedAtUtc = requestedAtUtc,
            Version = current.Version + 1,
            UpdatedAtUtc = requestedAtUtc,
        };
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = {desired.Status}, checkpoint_code = {desired.Checkpoint},
                failure_collection_code = NULL, failure_code = NULL, failure_message = NULL,
                delivery_idempotency_key = {idempotencyKey},
                delivery_provider_collection_code = {MasterDataCodes.EmailProviders.Collection},
                delivery_provider_code = {providerCode},
                delivery_requested_at_utc = {requestedAtUtc},
                version = version + 1, updated_at_utc = {requestedAtUtc}
            WHERE tenant_id = {tenantId.Value} AND inbound_email_id = {inboundEmailId}
              AND version = {current.Version}
              AND checkpoint_code = {MasterDataCodes.EmailAutomationCheckpoints.DocumentRendered}
              AND document_id IS NOT NULL AND delivery_requested_at_utc IS NULL
            """, cancellationToken);
        if (changed != 1)
        {
            var winner = await FindRunAsync(tenantId, inboundEmailId, cancellationToken);
            if (winner?.DeliveryRequestedAtUtc.HasValue != true)
            {
                throw new VersionConflictException();
            }
            EnsureIntentMatches(winner, providerCode, idempotencyKey);
            await transaction.CommitAsync(cancellationToken);
            return new DeliveryIntentResult(winner, false);
        }

        AddTransition(tenantId, actorId, desired, new EmailAutomationTransition(
            new CommandId(Guid.NewGuid()),
            correlationId,
            MasterDataReferences.CommercialActions.EmailAutomationDeliveryRequested,
            MasterDataReferences.CommercialEventTypes.EmailProposalDeliveryRequested));
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryIntentResult(desired, true);
    }

    internal async Task<EmailAutomationRunRow> RecordDeliveryAcceptanceAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        string providerMessageId,
        DateTimeOffset acceptedAtUtc,
        DateTimeOffset recordedAtUtc,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var current = await FindRunAsync(tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        if (current.DeliveryAcceptedAtUtc.HasValue)
        {
            EnsureReceiptMatches(current, providerMessageId);
            await transaction.CommitAsync(cancellationToken);
            return current;
        }
        if (!current.DeliveryRequestedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Delivery acceptance has no durable request.");
        }

        var desired = current with
        {
            Status = MasterDataCodes.EmailAutomationStatuses.Processing,
            Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.DeliveryAccepted,
            FailureCode = null,
            FailureMessage = null,
            DeliveryProviderId = providerMessageId,
            DeliveryAcceptedAtUtc = acceptedAtUtc,
            Version = current.Version + 1,
            UpdatedAtUtc = recordedAtUtc,
        };
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = {desired.Status}, checkpoint_code = {desired.Checkpoint},
                failure_collection_code = NULL, failure_code = NULL, failure_message = NULL,
                delivery_provider_id = {providerMessageId},
                delivery_accepted_at_utc = {acceptedAtUtc},
                version = version + 1, updated_at_utc = {recordedAtUtc}
            WHERE tenant_id = {tenantId.Value} AND inbound_email_id = {inboundEmailId}
              AND version = {current.Version}
              AND checkpoint_code = {MasterDataCodes.EmailAutomationCheckpoints.DeliveryRequested}
              AND delivery_requested_at_utc IS NOT NULL
              AND delivery_provider_id IS NULL AND delivery_accepted_at_utc IS NULL
            """, cancellationToken);
        if (changed != 1)
        {
            var winner = await FindRunAsync(tenantId, inboundEmailId, cancellationToken);
            if (winner?.DeliveryAcceptedAtUtc.HasValue != true)
            {
                throw new VersionConflictException();
            }
            EnsureReceiptMatches(winner, providerMessageId);
            await transaction.CommitAsync(cancellationToken);
            return winner;
        }

        AddTransition(tenantId, actorId, desired, new EmailAutomationTransition(
            new CommandId(Guid.NewGuid()),
            correlationId,
            MasterDataReferences.CommercialActions.EmailAutomationDeliveryAccepted,
            MasterDataReferences.CommercialEventTypes.EmailProposalDeliveryAccepted));
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return desired;
    }

    private static void EnsureIntentMatches(
        EmailAutomationRunRow run,
        string providerCode,
        string idempotencyKey)
    {
        if (run.DeliveryProviderCode != providerCode ||
            run.DeliveryIdempotencyKey != idempotencyKey)
        {
            throw new InvalidOperationException("The durable delivery intent does not match.");
        }
    }

    private static void EnsureReceiptMatches(
        EmailAutomationRunRow run,
        string providerMessageId)
    {
        if (run.DeliveryProviderId != providerMessageId)
        {
            throw new InvalidOperationException("The durable delivery receipt does not match.");
        }
    }
}

internal sealed record DeliveryIntentResult(
    EmailAutomationRunRow Run,
    bool ShouldSend);
