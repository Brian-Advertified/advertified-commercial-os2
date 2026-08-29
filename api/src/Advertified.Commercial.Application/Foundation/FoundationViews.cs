namespace Advertified.Commercial.Application.Foundation;

public sealed record TenantView(
    Guid Id,
    string TypeCode,
    string LegalName,
    string TradingName,
    string Slug,
    string StatusCode,
    string TimeZone,
    string CurrencyCode,
    string VatStatusCode,
    string? VatNumber,
    string SettingsJson,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record MembershipView(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string RoleCode,
    string StatusCode,
    Guid? InvitedBy,
    DateTimeOffset InvitedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClientAccountView(
    Guid Id,
    Guid TenantId,
    string ExternalReference,
    string LegalName,
    string TradingName,
    string? Website,
    string? Industry,
    string BillingProfileJson,
    Guid? PrimaryContactId,
    string StatusCode,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgencyView(
    Guid Id,
    Guid TenantId,
    string ExternalReference,
    string LegalName,
    string TradingName,
    string? Website,
    string StatusCode,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record ContactView(
    Guid Id,
    Guid TenantId,
    Guid ClientAccountId,
    string Name,
    string? JobTitle,
    string Email,
    string? Phone,
    string PurposeCode,
    string ConsentBasis,
    DateOnly? RetainUntil,
    string StatusCode,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);

public sealed record CommandResult<T>(
    T Data,
    long Version,
    bool Replayed);
