using Advertified.Commercial.Application.Outbox;

namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed record OutboxDispatchClaim(
    OutboxDeliveryEnvelope Envelope,
    Guid ClaimToken,
    int Attempt,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record OutboxDispatchDeadLetter(
    Guid EventId,
    Guid CorrelationId,
    int Attempt,
    string FailureCode);

public sealed record OutboxDispatchSelection(
    OutboxDispatchClaim? Claim,
    OutboxDispatchDeadLetter? DeadLetter);

internal sealed class OutboxDispatchClaimRow
{
    public Guid EventId { get; set; }

    public Guid TenantId { get; set; }

    public Guid CausationId { get; set; }

    public Guid CorrelationId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string AggregateType { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public long AggregateVersion { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? ClaimToken { get; set; }

    public int Attempt { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public bool DeadLetteredOnClaim { get; set; }

    public string? FailureCode { get; set; }
}
