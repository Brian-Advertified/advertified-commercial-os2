namespace Advertified.Commercial.Domain.Governance;

public sealed record CommandEnvelope<TCommand> where TCommand : notnull
{
    public CommandEnvelope(
        TenantId tenantId,
        ActorId actorId,
        CommandId commandId,
        CorrelationId correlationId,
        IdempotencyKey idempotencyKey,
        Sha256Digest payloadHash,
        long expectedVersion,
        DateTimeOffset requestedAtUtc,
        TCommand command)
    {
        IdValue.Require(tenantId.Value, nameof(tenantId));
        IdValue.Require(actorId.Value, nameof(actorId));
        IdValue.Require(commandId.Value, nameof(commandId));
        IdValue.Require(correlationId.Value, nameof(correlationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash.Value);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentNullException.ThrowIfNull(command);
        UtcValue.Require(requestedAtUtc, nameof(requestedAtUtc));

        TenantId = tenantId;
        ActorId = actorId;
        CommandId = commandId;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        PayloadHash = payloadHash;
        ExpectedVersion = expectedVersion;
        RequestedAtUtc = requestedAtUtc;
        Command = command;
    }

    public TenantId TenantId { get; }

    public ActorId ActorId { get; }

    public CommandId CommandId { get; }

    public CorrelationId CorrelationId { get; }

    public IdempotencyKey IdempotencyKey { get; }

    public Sha256Digest PayloadHash { get; }

    public long ExpectedVersion { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public TCommand Command { get; }
}
