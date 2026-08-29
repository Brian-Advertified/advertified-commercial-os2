namespace Advertified.Commercial.Domain.Governance;

public readonly record struct TenantTypeCode
{
    public TenantTypeCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct LifecycleStatusCode
{
    public LifecycleStatusCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct RoleCode
{
    public RoleCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct CurrencyCode
{
    public CurrencyCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct VatStatusCode
{
    public VatStatusCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct ContactPurposeCode
{
    public ContactPurposeCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct PermissionCode
{
    public PermissionCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct ActionCode
{
    public ActionCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct EventTypeCode
{
    public EventTypeCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

public readonly record struct ResourceTypeCode
{
    public ResourceTypeCode(string value)
    {
        Value = CodeValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}

internal static class CodeValue
{
    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();

        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A stable code cannot exceed 100 characters.");
        }

        return normalized;
    }
}
