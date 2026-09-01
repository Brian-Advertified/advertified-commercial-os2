using System.Collections.Concurrent;
using Advertified.Commercial.Application.Outbox;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed class DeterministicOutboxTransport(
    IOptions<OutboxDispatchOptions> options) : IOutboxTransport
{
    private const string UnavailableCode = "DETERMINISTIC_TRANSPORT_UNAVAILABLE";
    private readonly ConcurrentDictionary<Guid, string> acceptedEvents = new();
    private readonly ConcurrentDictionary<Guid, int> publishAttempts = new();

    public int AcceptedEventCount => acceptedEvents.Count;

    public int AttemptsFor(Guid eventId) =>
        publishAttempts.TryGetValue(eventId, out var attempts) ? attempts : 0;

    public ValueTask<OutboxTransportHealth> CheckHealthAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(options.Value.DeterministicTransportAvailable
            ? OutboxTransportHealth.Available()
            : OutboxTransportHealth.Unavailable(UnavailableCode));
    }

    public Task<OutboxPublishResult> PublishAsync(
        OutboxDeliveryEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        publishAttempts.AddOrUpdate(envelope.IdempotencyKey, 1, (_, value) => value + 1);
        if (!options.Value.DeterministicTransportAvailable)
        {
            return Task.FromResult(
                OutboxPublishResult.TransientFailure(UnavailableCode));
        }

        var reference = acceptedEvents.GetOrAdd(
            envelope.IdempotencyKey,
            static eventId => $"deterministic:{eventId:D}");
        return Task.FromResult(OutboxPublishResult.Accepted(reference));
    }
}

public sealed class DisabledOutboxTransport : IOutboxTransport
{
    public ValueTask<OutboxTransportHealth> CheckHealthAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            OutboxTransportHealth.Unavailable("OUTBOX_DISPATCH_DISABLED"));
    }

    public Task<OutboxPublishResult> PublishAsync(
        OutboxDeliveryEnvelope envelope,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Outbox dispatch is disabled.");
}
