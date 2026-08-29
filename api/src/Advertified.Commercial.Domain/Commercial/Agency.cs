using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class Agency
{
    private Agency()
    {
    }

    public Agency(
        AgencyId id,
        TenantId tenantId,
        string externalReference,
        string legalName,
        string tradingName,
        string? website,
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
        Status = status;
        CreatedAtUtc = CommercialValue.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public AgencyId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string ExternalReference { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string TradingName { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
