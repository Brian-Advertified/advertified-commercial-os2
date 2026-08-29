namespace Advertified.Commercial.Domain.Governance;

public readonly record struct TenantId
{
    public TenantId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct UserId
{
    public UserId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct MembershipId
{
    public MembershipId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct ClientAccountId
{
    public ClientAccountId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct AgencyId
{
    public AgencyId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct ContactId
{
    public ContactId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct ActorId
{
    public ActorId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct CommandId
{
    public CommandId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct CorrelationId
{
    public CorrelationId(Guid value)
    {
        Value = IdValue.Require(value, nameof(value));
    }

    public Guid Value { get; }
}

public readonly record struct IdempotencyKey
{
    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();

        if (normalized.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Idempotency key is too long.");
        }

        Value = normalized;
    }

    public string Value { get; }
}

public readonly record struct Sha256Digest
{
    public Sha256Digest(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A lowercase or uppercase SHA-256 hex digest is required.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}

internal static class IdValue
{
    public static Guid Require(Guid value, string parameterName)
    {
        return value == Guid.Empty
            ? throw new ArgumentException("A non-empty identifier is required.", parameterName)
            : value;
    }
}
