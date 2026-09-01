using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OutboxDispatchProcessor(
    OutboxDispatchStore store,
    IOutboxTransport transport,
    IOptions<OutboxDispatchOptions> options,
    TimeProvider timeProvider,
    OutboxDispatchMetrics metrics,
    ILogger<OutboxDispatchProcessor> logger)
{
    private const string UnexpectedTransportFailure = "OUTBOX_TRANSPORT_FAILURE";
    private const string TransportTimeout = "OUTBOX_TRANSPORT_TIMEOUT";

    public async Task<bool> ProcessNextAsync(
        Guid tenantId,
        Guid workerId,
        CancellationToken cancellationToken)
    {
        var selection = await store.ClaimNextAsync(
            tenantId,
            workerId,
            options.Value.LeaseSeconds,
            cancellationToken);
        if (selection is null)
        {
            return false;
        }
        if (selection.DeadLetter is { } deadLetter)
        {
            RecordClaimDeadLetter(deadLetter);
            return true;
        }

        var claim = selection.Claim ?? throw new InvalidOperationException(
            "The outbox dispatch selection contains no outcome.");

        using var heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutCancellation =
            new CancellationTokenSource(options.Value.PublishTimeout, timeProvider);
        using var publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        var heartbeat = MaintainLeaseAsync(claim, heartbeatCancellation.Token);
        var result = await PublishSafelyAsync(
            claim.Envelope,
            publishCancellation.Token,
            cancellationToken);
        heartbeatCancellation.Cancel();
        var leaseHeld = await heartbeat;
        cancellationToken.ThrowIfCancellationRequested();
        if (!leaseHeld)
        {
            RecordLeaseLost(claim);
            return true;
        }

        await RecordOutcomeAsync(claim, result, cancellationToken);
        return true;
    }

    private async Task<OutboxPublishResult> PublishSafelyAsync(
        OutboxDeliveryEnvelope envelope,
        CancellationToken publishToken,
        CancellationToken stoppingToken)
    {
        try
        {
            return await transport.PublishAsync(envelope, publishToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (publishToken.IsCancellationRequested)
        {
            return OutboxPublishResult.TransientFailure(TransportTimeout);
        }
        catch
        {
            return OutboxPublishResult.TransientFailure(UnexpectedTransportFailure);
        }
    }

    private async Task RecordOutcomeAsync(
        OutboxDispatchClaim claim,
        OutboxPublishResult result,
        CancellationToken cancellationToken)
    {
        if (result.Disposition == OutboxPublishDisposition.Accepted)
        {
            await RecordAcceptanceAsync(claim, result, cancellationToken);
            return;
        }

        await RecordFailureAsync(claim, result, cancellationToken);
    }

    private void RecordLeaseLost(OutboxDispatchClaim claim)
    {
        metrics.RecordLeaseLost();
        LogLeaseLost(
            logger,
            claim.Envelope.EventId,
            claim.Envelope.CorrelationId,
            claim.Attempt);
    }

    private void RecordClaimDeadLetter(OutboxDispatchDeadLetter deadLetter)
    {
        metrics.RecordDeadLetter();
        LogDeadLettered(
            logger,
            deadLetter.EventId,
            deadLetter.CorrelationId,
            deadLetter.Attempt,
            deadLetter.FailureCode);
    }

    [LoggerMessage(
        EventId = 12_104,
        Level = LogLevel.Warning,
        Message = "Outbox claim was fenced before completion. EventId={EventId} " +
            "CorrelationId={CorrelationId} Attempt={Attempt}")]
    private static partial void LogLeaseLost(
        ILogger logger,
        Guid eventId,
        Guid correlationId,
        int attempt);
}
