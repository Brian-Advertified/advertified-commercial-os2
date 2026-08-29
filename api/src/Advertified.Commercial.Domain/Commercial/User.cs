using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class User
{
    private User()
    {
    }

    public User(
        UserId id,
        EmailAddress email,
        string displayName,
        string? phone,
        LifecycleStatusCode status,
        bool mfaEnabled,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        DisplayName = CommercialValue.Required(displayName, 200, nameof(displayName));
        Phone = CommercialValue.Optional(phone, 50, nameof(phone));
        Status = status;
        MfaEnabled = mfaEnabled;
        CreatedAtUtc = CommercialValue.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public UserId Id { get; private set; }

    public EmailAddress Email { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public bool MfaEnabled { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void RecordLogin(DateTimeOffset loggedInAtUtc, long expectedVersion)
    {
        Version = AggregateVersion.Next(Version, expectedVersion);
        LastLoginAtUtc = CommercialValue.Utc(loggedInAtUtc, nameof(loggedInAtUtc));
        UpdatedAtUtc = LastLoginAtUtc.Value;
    }

    public void UpdateProfile(
        string displayName,
        string? phone,
        long expectedVersion,
        DateTimeOffset updatedAtUtc)
    {
        Version = AggregateVersion.Next(Version, expectedVersion);
        DisplayName = CommercialValue.Required(displayName, 200, nameof(displayName));
        Phone = CommercialValue.Optional(phone, 50, nameof(phone));
        UpdatedAtUtc = CommercialValue.Utc(updatedAtUtc, nameof(updatedAtUtc));
    }
}
