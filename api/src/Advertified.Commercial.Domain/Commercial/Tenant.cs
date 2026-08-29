using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Tenant(
        TenantId id,
        TenantTypeCode type,
        string legalName,
        string tradingName,
        Slug slug,
        LifecycleStatusCode status,
        string timeZone,
        CurrencyCode currency,
        VatStatusCode vatStatus,
        string? vatNumber,
        string settingsJson,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Type = type;
        LegalName = CommercialValue.Required(legalName, 200, nameof(legalName));
        TradingName = CommercialValue.Required(tradingName, 200, nameof(tradingName));
        Slug = slug;
        Status = status;
        TimeZone = CommercialValue.Required(timeZone, 100, nameof(timeZone));
        Currency = currency;
        VatStatus = vatStatus;
        VatNumber = CommercialValue.Optional(vatNumber, 50, nameof(vatNumber));
        SettingsJson = CommercialValue.JsonObject(settingsJson, nameof(settingsJson));
        CreatedAtUtc = CommercialValue.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public TenantId Id { get; private set; }

    public TenantTypeCode Type { get; private set; }

    public string LegalName { get; private set; } = string.Empty;

    public string TradingName { get; private set; } = string.Empty;

    public Slug Slug { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    public CurrencyCode Currency { get; private set; }

    public VatStatusCode VatStatus { get; private set; }

    public string? VatNumber { get; private set; }

    public string SettingsJson { get; private set; } = "{}";

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateProfile(
        string legalName,
        string tradingName,
        string settingsJson,
        long expectedVersion,
        DateTimeOffset updatedAtUtc)
    {
        Version = AggregateVersion.Next(Version, expectedVersion);
        LegalName = CommercialValue.Required(legalName, 200, nameof(legalName));
        TradingName = CommercialValue.Required(tradingName, 200, nameof(tradingName));
        SettingsJson = CommercialValue.JsonObject(settingsJson, nameof(settingsJson));
        UpdatedAtUtc = CommercialValue.Utc(updatedAtUtc, nameof(updatedAtUtc));
    }
}
