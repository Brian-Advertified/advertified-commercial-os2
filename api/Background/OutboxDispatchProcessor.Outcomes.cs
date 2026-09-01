using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OutboxDispatchProcessor
{
    private async Task RecordAcceptanceAsync(
        OutboxDispatchClaim claim,
        OutboxPublishResult result,
        CancellationToken cancellationToken)
    {
        var recorded = await store.AcknowledgeAsync(
            claim.Envelope.TenantId,
            claim.Envelope.EventId,
            claim.ClaimToken,
            result.TransportReference!,
            cancellationToken);
        if (!recorded)
        {
            RecordLeaseLost(claim);
            return;
        }

        metrics.RecordAccepted();
        LogAccepted(
            logger,
            claim.Envelope.EventId,
            claim.Envelope.CorrelationId,
            claim.Attempt);
    }

    private async Task RecordFailureAsync(
        OutboxDispatchClaim claim,
        OutboxPublishResult result,
        CancellationToken cancellationToken)
    {
        var terminal = result.Disposition == OutboxPublishDisposition.TerminalFailure;
        var recorded = await store.FailAsync(
            claim.Envelope.TenantId,
            claim.Envelope.EventId,
            claim.ClaimToken,
            terminal,
            result.FailureCode!,
            cancellationToken);
        if (!recorded)
        {
            RecordLeaseLost(claim);
            return;
        }

        if (terminal || claim.Attempt >= 4)
        {
            metrics.RecordDeadLetter();
            LogDeadLettered(logger, claim.Envelope.EventId,
                claim.Envelope.CorrelationId, claim.Attempt, result.FailureCode!);
            return;
        }

        metrics.RecordRetry();
        LogRetryScheduled(logger, claim.Envelope.EventId,
            claim.Envelope.CorrelationId, claim.Attempt, result.FailureCode!);
    }

    [LoggerMessage(
        EventId = 12_101,
        Level = LogLevel.Information,
        Message = "Outbox event accepted. EventId={EventId} CorrelationId={CorrelationId} " +
            "Attempt={Attempt}")]
    private static partial void LogAccepted(
        ILogger logger,
        Guid eventId,
        Guid correlationId,
        int attempt);

    [LoggerMessage(
        EventId = 12_102,
        Level = LogLevel.Warning,
        Message = "Outbox retry scheduled. EventId={EventId} CorrelationId={CorrelationId} " +
            "Attempt={Attempt} FailureCode={FailureCode}")]
    private static partial void LogRetryScheduled(
        ILogger logger,
        Guid eventId,
        Guid correlationId,
        int attempt,
        string failureCode);

    [LoggerMessage(
        EventId = 12_103,
        Level = LogLevel.Error,
        Message = "Outbox event dead-lettered. EventId={EventId} CorrelationId={CorrelationId} " +
            "Attempt={Attempt} FailureCode={FailureCode}")]
    private static partial void LogDeadLettered(
        ILogger logger,
        Guid eventId,
        Guid correlationId,
        int attempt,
        string failureCode);
}
