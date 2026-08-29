using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class ClientAccount
{
    private ClientAccount()
    {
    }

    public ClientAccount(
        ClientAccountId id,
        TenantId tenantId,
        string externalReference,
        string legalName,
        string tradingName,
        string? website,
        string? industry,
        string billingProfileJson,
        LifecycleStatusCode status,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ExternalReference = CommercialValue.Required(
            externalReference,
            100,
            nameof(externalReference));
        LegalName = CommercialValue.Required(legalName, 200, nameof(legalName));
        TradingName = CommercialValue.Required(tradingName, 200, nameof(tradingName));
        Website = CommercialValue.Website(website, nameof(website));
        Industry = CommercialValue.Optional(industry, 100, nameof(industry));
        BillingProfileJson = CommercialValue.JsonObject(
            billingProfileJson,
            nameof(billingProfileJson));
        Status = status;
        CreatedAtUtc = CommercialValue.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public ClientAccountId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string ExternalReference { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string TradingName { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string? Industry { get; private set; }

    public string BillingProfileJson { get; private set; } = "{}";

    public ContactId? PrimaryContactId { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void SetPrimaryContact(
        ContactId contactId,
        long expectedVersion,
        DateTimeOffset updatedAtUtc)
    {
        Version = AggregateVersion.Next(Version, expectedVersion);
        PrimaryContactId = contactId;
        UpdatedAtUtc = CommercialValue.Utc(updatedAtUtc, nameof(updatedAtUtc));
    }
}
