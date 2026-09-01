using System.Text.Json;

namespace Advertified.Commercial.Application.Outbox;

public sealed record OutboxDeliveryEnvelope(
    Guid EventId,
    Guid TenantId,
    Guid CausationId,
    Guid CorrelationId,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    JsonElement Payload,
    DateTimeOffset OccurredAtUtc)
{
    public Guid IdempotencyKey => EventId;
}

public enum OutboxPublishDisposition
{
    Accepted,
    TransientFailure,
    TerminalFailure,
}

public sealed record OutboxPublishResult
{
    public const int MaximumTransportReferenceLength = 300;
    public const int MaximumFailureCodeLength = 100;

    private OutboxPublishResult(
        OutboxPublishDisposition disposition,
        string? transportReference,
        string? failureCode)
    {
        Disposition = disposition;
        TransportReference = transportReference;
        FailureCode = failureCode;
    }

    public OutboxPublishDisposition Disposition { get; }

    public string? TransportReference { get; }

    public string? FailureCode { get; }

    public static OutboxPublishResult Accepted(string transportReference) => new(
        OutboxPublishDisposition.Accepted,
        RequireValue(transportReference, MaximumTransportReferenceLength),
        null);

    public static OutboxPublishResult TransientFailure(string failureCode) => new(
        OutboxPublishDisposition.TransientFailure,
        null,
        RequireSafeCode(failureCode));

    public static OutboxPublishResult TerminalFailure(string failureCode) => new(
        OutboxPublishDisposition.TerminalFailure,
        null,
        RequireSafeCode(failureCode));

    private static string RequireValue(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException("The transport value is invalid.", nameof(value));
        }

        return value;
    }

    private static string RequireSafeCode(string value)
    {
        var code = RequireValue(value, MaximumFailureCodeLength);
        if (!char.IsAsciiLetterOrDigit(code[0]) ||
            !code.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-' or '.' or ':'))
        {
            throw new ArgumentException("The transport failure code is invalid.", nameof(value));
        }

        return code;
    }
}

public sealed record OutboxTransportHealth
{
    private OutboxTransportHealth(bool isAvailable, string? failureCode)
    {
        IsAvailable = isAvailable;
        FailureCode = failureCode;
    }

    public bool IsAvailable { get; }

    public string? FailureCode { get; }

    public static OutboxTransportHealth Available() => new(true, null);

    public static OutboxTransportHealth Unavailable(string failureCode)
    {
        var result = OutboxPublishResult.TransientFailure(failureCode);
        return new(false, result.FailureCode);
    }
}

public interface IOutboxTransport
{
    ValueTask<OutboxTransportHealth> CheckHealthAsync(
        CancellationToken cancellationToken);

    Task<OutboxPublishResult> PublishAsync(
        OutboxDeliveryEnvelope envelope,
        CancellationToken cancellationToken);
}
