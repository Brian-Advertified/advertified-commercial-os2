using System.Diagnostics.Metrics;

namespace Advertified.Commercial.Api.Background;

public sealed class OutboxDispatchMetrics
{
    private const string MeterName = "Advertified.Commercial.Outbox";
    private static readonly Meter DispatchMeter = new(MeterName);
    private static readonly Counter<long> Accepted =
        DispatchMeter.CreateCounter<long>("advertified.outbox.accepted");
    private static readonly Counter<long> Retried =
        DispatchMeter.CreateCounter<long>("advertified.outbox.retry_scheduled");
    private static readonly Counter<long> DeadLettered =
        DispatchMeter.CreateCounter<long>("advertified.outbox.dead_lettered");
    private static readonly Counter<long> LeaseLost =
        DispatchMeter.CreateCounter<long>("advertified.outbox.lease_lost");

    public void RecordAccepted() => Accepted.Add(1);

    public void RecordRetry() => Retried.Add(1);

    public void RecordDeadLetter() => DeadLettered.Add(1);

    public void RecordLeaseLost() => LeaseLost.Add(1);
}
