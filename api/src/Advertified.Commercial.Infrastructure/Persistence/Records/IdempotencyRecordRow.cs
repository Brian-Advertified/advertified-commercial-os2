using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Persistence.Records;

public sealed class IdempotencyRecordRow
{
    private IdempotencyRecordRow()
    {
    }

    public IdempotencyRecordRow(
        TenantId tenantId,
        IdempotencyKey key,
        CommandId commandId,
        Sha256Digest requestHash,
        string outcomeJson,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        TenantId = tenantId;
        Key = key;
        CommandId = commandId;
        RequestHash = requestHash;
        OutcomeJson = outcomeJson;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public TenantId TenantId { get; private set; }

    public IdempotencyKey Key { get; private set; }

    public CommandId CommandId { get; private set; }

    public Sha256Digest RequestHash { get; private set; }

    public string OutcomeJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }
}
