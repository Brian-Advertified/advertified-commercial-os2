using System.Diagnostics.Metrics;
using Advertified.Commercial.Application.Outbox;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OutboxDispatchAcceptanceTests
{
    private sealed class ScriptedOutboxTransport(
        IEnumerable<OutboxPublishResult> results) : IOutboxTransport
    {
        private readonly Queue<OutboxPublishResult> results = new(results);

        public ValueTask<OutboxTransportHealth> CheckHealthAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(OutboxTransportHealth.Available());

        public Task<OutboxPublishResult> PublishAsync(
            OutboxDeliveryEnvelope envelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class BlockingOutboxTransport : IOutboxTransport
    {
        public ValueTask<OutboxTransportHealth> CheckHealthAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(OutboxTransportHealth.Available());

        public async Task<OutboxPublishResult> PublishAsync(
            OutboxDeliveryEnvelope envelope,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocking test transport resumed unexpectedly.");
        }
    }

    private sealed class ControlledOutboxTransport : IOutboxTransport
    {
        private readonly TaskCompletionSource started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<OutboxPublishResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilStartedAsync() =>
            started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Accept(Guid eventId) => completion.TrySetResult(
            OutboxPublishResult.Accepted($"controlled:{eventId:D}"));

        public ValueTask<OutboxTransportHealth> CheckHealthAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(OutboxTransportHealth.Available());

        public Task<OutboxPublishResult> PublishAsync(
            OutboxDeliveryEnvelope envelope,
            CancellationToken cancellationToken)
        {
            started.TrySetResult();
            return completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class OutboxCounterObserver(string instrumentName) : IDisposable
    {
        private const string MeterName = "Advertified.Commercial.Outbox";
        private readonly MeterListener listener = CreateListener(instrumentName);
        private long total;

        public long Total => Interlocked.Read(ref total);

        public void Start()
        {
            listener.SetMeasurementEventCallback<long>(Record);
            listener.Start();
        }

        public void Dispose() => listener.Dispose();

        private static MeterListener CreateListener(string expectedInstrument)
        {
            var created = new MeterListener();
            created.InstrumentPublished = (instrument, activeListener) =>
            {
                if (instrument.Meter.Name == MeterName &&
                    instrument.Name == expectedInstrument)
                {
                    activeListener.EnableMeasurementEvents(instrument);
                }
            };
            return created;
        }

        private void Record(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state) => Interlocked.Add(ref total, measurement);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
