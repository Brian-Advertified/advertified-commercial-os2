using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationCommands
{
    private async Task<CommandResult<EmailAutomationRunView>> ContinuePreparedRunAsync<TCommand>(
        Guid inboundEmailId,
        CommandEnvelope<TCommand> envelope,
        CommandReceipt receipt,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        if (receipt.Disposition == CommandDisposition.Replayed)
        {
            var current = await ReadAuthorizedRunAsync(
                inboundEmailId, envelope, cancellationToken);
            if (current.Status != MasterDataCodes.EmailAutomationStatuses.Processing)
            {
                return new CommandResult<EmailAutomationRunView>(
                    EmailAutomationRecordStore.ToView(current), current.Version, true);
            }
        }
        var view = await processor.ProcessAsync(
            envelope.TenantId,
            envelope.ActorId,
            inboundEmailId,
            envelope.CorrelationId,
            cancellationToken);
        return new CommandResult<EmailAutomationRunView>(
            view, view.Version, receipt.Disposition == CommandDisposition.Replayed);
    }

    private async Task<EmailAutomationRunRow> ReadAuthorizedRunAsync<TCommand>(
        Guid inboundEmailId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        await EnsureAllowedAsync(
            envelope.ActorId,
            envelope.TenantId,
            MasterDataReferences.Permissions.EmailAutomationManage,
            cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            envelope.ActorId, envelope.TenantId, cancellationToken);
        var run = await store.FindRunAsync(
            envelope.TenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        await transaction.CommitAsync(cancellationToken);
        return run;
    }

    private async Task<CommandOutcome> PrepareProcessOutcomeAsync(
        Guid inboundEmailId,
        CommandEnvelope<ProcessInboundEmailCommand> envelope,
        CancellationToken cancellationToken)
    {
        var run = await store.FindRunAsync(
            envelope.TenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        if (run.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if ((run.Status is MasterDataCodes.EmailAutomationStatuses.ReviewRequired or
                MasterDataCodes.EmailAutomationStatuses.Failed) &&
            !run.DeliveryRequestedAtUtc.HasValue)
        {
            throw new EmailAutomationNotRetryableException();
        }
        if (run.Status == MasterDataCodes.EmailAutomationStatuses.Failed &&
            run.FailureCode == MasterDataCodes.AutomationFailureReasons.DeliveryFailed &&
            run.DeliveryRequestedAtUtc.HasValue)
        {
            throw new EmailAutomationNotRetryableException();
        }
        return await MarkProcessingAsync(
            run, envelope, run.UnderstandingJson, run.ClarificationsJson, cancellationToken);
    }

    private async Task<CommandOutcome> PrepareRetryOutcomeAsync(
        Guid inboundEmailId,
        CommandEnvelope<RetryInboundEmailCommand> envelope,
        EmailAutomationClarificationInput[] clarifications,
        CancellationToken cancellationToken)
    {
        var run = await store.FindRunAsync(
            envelope.TenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        if (run.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if (run.Status is not (MasterDataCodes.EmailAutomationStatuses.ReviewRequired or
                MasterDataCodes.EmailAutomationStatuses.Failed) ||
            run.DeliveryRequestedAtUtc.HasValue)
        {
            throw new EmailAutomationNotRetryableException();
        }
        EnsureClarificationsAllowed(run, clarifications);
        var clarificationsJson = clarifications.Length == 0
            ? run.ClarificationsJson
            : EmailAutomationRecordStore.Write(clarifications);
        var understandingJson = clarifications.Length == 0
            ? run.UnderstandingJson
            : null;
        return await MarkProcessingAsync(
            run, envelope, understandingJson, clarificationsJson, cancellationToken);
    }

    private async Task<CommandOutcome> MarkProcessingAsync<TCommand>(
        EmailAutomationRunRow run,
        CommandEnvelope<TCommand> envelope,
        string? understandingJson,
        string clarificationsJson,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = {MasterDataCodes.EmailAutomationStatuses.Processing},
                understanding_json = {understandingJson}::jsonb,
                clarifications_json = {clarificationsJson}::jsonb,
                failure_collection_code = NULL, failure_code = NULL,
                failure_message = NULL, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value}
              AND inbound_email_id = {run.InboundEmailId}
              AND version = {run.Version}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = run with
        {
            Status = MasterDataCodes.EmailAutomationStatuses.Processing,
            UnderstandingJson = understandingJson,
            ClarificationsJson = clarificationsJson,
            FailureCode = null,
            FailureMessage = null,
            Version = run.Version + 1,
            UpdatedAtUtc = now,
        };
        return OpportunityCommandSupport.Outcome(
            envelope,
            EmailAutomationRecordStore.ToView(updated),
            updated.Id,
            updated.Version,
            MasterDataReferences.CommercialResourceTypes.EmailProposalAutomationRun,
            MasterDataReferences.CommercialActions.EmailAutomationStarted,
            MasterDataReferences.CommercialEventTypes.EmailProposalAutomationStarted,
            now);
    }
}
