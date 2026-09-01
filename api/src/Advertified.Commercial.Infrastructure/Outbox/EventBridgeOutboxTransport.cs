using System.Net;
using System.Text.Json;
using Amazon;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Advertified.Commercial.Application.Outbox;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed class EventBridgeOutboxTransport : IOutboxTransport, IDisposable
{
    private const string Unavailable = "EVENTBRIDGE_UNAVAILABLE";
    private const string Rejected = "EVENTBRIDGE_REJECTED";
    private readonly AmazonEventBridgeClient client;
    private readonly OutboxDispatchOptions options;

    public EventBridgeOutboxTransport(IOptions<OutboxDispatchOptions> options)
    {
        this.options = options.Value;
        if (!OutboxDispatchOptions.HasSafeTransportConfiguration(this.options))
        {
            throw new InvalidOperationException("EventBridge transport configuration is unsafe.");
        }
        client = new AmazonEventBridgeClient(
            RegionEndpoint.GetBySystemName(this.options.AwsRegion!));
    }

    public async ValueTask<OutboxTransportHealth> CheckHealthAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DescribeEventBusAsync(
                new DescribeEventBusRequest { Name = options.EventBusName },
                cancellationToken);
            return OutboxTransportHealth.Available();
        }
        catch (AmazonEventBridgeException)
        {
            return OutboxTransportHealth.Unavailable(Unavailable);
        }
    }

    public async Task<OutboxPublishResult> PublishAsync(
        OutboxDeliveryEnvelope envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.PutEventsAsync(
                new PutEventsRequest
                {
                    Entries =
                    [
                        new PutEventsRequestEntry
                        {
                            EventBusName = options.EventBusName,
                            Source = options.EventSource,
                            DetailType = envelope.EventType,
                            Time = envelope.OccurredAtUtc.UtcDateTime,
                            Detail = Serialize(envelope),
                        },
                    ],
                },
                cancellationToken);
            var entry = response.Entries.SingleOrDefault();
            if (response.FailedEntryCount == 0 &&
                !string.IsNullOrWhiteSpace(entry?.EventId))
            {
                return OutboxPublishResult.Accepted(entry.EventId);
            }
            return IsTransient(entry?.ErrorCode)
                ? OutboxPublishResult.TransientFailure(Unavailable)
                : OutboxPublishResult.TerminalFailure(Rejected);
        }
        catch (AmazonEventBridgeException exception) when (IsTransient(exception.StatusCode))
        {
            return OutboxPublishResult.TransientFailure(Unavailable);
        }
        catch (AmazonEventBridgeException)
        {
            return OutboxPublishResult.TerminalFailure(Rejected);
        }
    }

    public void Dispose() => client.Dispose();

    private static string Serialize(OutboxDeliveryEnvelope envelope) =>
        JsonSerializer.Serialize(new
        {
            eventId = envelope.EventId,
            tenantId = envelope.TenantId,
            causationId = envelope.CausationId,
            correlationId = envelope.CorrelationId,
            aggregateType = envelope.AggregateType,
            aggregateId = envelope.AggregateId,
            aggregateVersion = envelope.AggregateVersion,
            occurredAtUtc = envelope.OccurredAtUtc,
            payload = envelope.Payload,
        });

    private static bool IsTransient(string? errorCode) =>
        errorCode is "InternalFailure" or "ThrottlingException" or "ServiceUnavailable";

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)status >= 500;
}
