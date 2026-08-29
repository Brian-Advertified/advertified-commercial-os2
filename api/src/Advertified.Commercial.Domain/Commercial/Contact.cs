using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class Contact
{
    private Contact()
    {
    }

    public Contact(
        ContactId id,
        TenantId tenantId,
        ClientAccountId clientAccountId,
        string name,
        string? jobTitle,
        EmailAddress email,
        string? phone,
        ContactPurposeCode purpose,
        string consentBasis,
        DateOnly? retainUntil,
        LifecycleStatusCode status,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ClientAccountId = clientAccountId;
        Name = CommercialValue.Required(name, 200, nameof(name));
        JobTitle = CommercialValue.Optional(jobTitle, 100, nameof(jobTitle));
        Email = email;
        Phone = CommercialValue.Optional(phone, 50, nameof(phone));
        Purpose = purpose;
        ConsentBasis = CommercialValue.Required(consentBasis, 500, nameof(consentBasis));
        RetainUntil = retainUntil;
        Status = status;
        CreatedAtUtc = CommercialValue.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public ContactId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public ClientAccountId ClientAccountId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? JobTitle { get; private set; }

    public EmailAddress Email { get; private set; }

    public string? Phone { get; private set; }

    public ContactPurposeCode Purpose { get; private set; }

    public string ConsentBasis { get; private set; } = string.Empty;

    public DateOnly? RetainUntil { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
