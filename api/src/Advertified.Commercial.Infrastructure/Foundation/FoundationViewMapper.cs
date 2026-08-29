using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Commercial;

namespace Advertified.Commercial.Infrastructure.Foundation;

internal static class FoundationViewMapper
{
    public static TenantView ToView(Tenant item) => new(
        item.Id.Value,
        item.Type.Value,
        item.LegalName,
        item.TradingName,
        item.Slug.Value,
        item.Status.Value,
        item.TimeZone,
        item.Currency.Value,
        item.VatStatus.Value,
        item.VatNumber,
        item.SettingsJson,
        item.Version,
        item.UpdatedAtUtc);

    public static CurrentUserView ToView(User item) => new(
        item.Id.Value,
        item.Email.Value,
        item.DisplayName,
        item.Phone,
        item.MfaEnabled,
        item.Version);

    public static MembershipView ToView(Membership item) => new(
        item.Id.Value,
        item.TenantId.Value,
        item.UserId.Value,
        item.Role.Value,
        item.Status.Value,
        item.InvitedBy?.Value,
        item.InvitedAtUtc,
        item.AcceptedAtUtc,
        item.Version,
        item.UpdatedAtUtc);

    public static ClientAccountView ToView(ClientAccount item) => new(
        item.Id.Value,
        item.TenantId.Value,
        item.ExternalReference,
        item.LegalName,
        item.TradingName,
        item.Website,
        item.Industry,
        item.BillingProfileJson,
        item.PrimaryContactId?.Value,
        item.Status.Value,
        item.Version,
        item.UpdatedAtUtc);

    public static AgencyView ToView(Agency item) => new(
        item.Id.Value,
        item.TenantId.Value,
        item.ExternalReference,
        item.LegalName,
        item.TradingName,
        item.Website,
        item.Status.Value,
        item.Version,
        item.UpdatedAtUtc);

    public static ContactView ToView(Contact item) => new(
        item.Id.Value,
        item.TenantId.Value,
        item.ClientAccountId.Value,
        item.Name,
        item.JobTitle,
        item.Email.Value,
        item.Phone,
        item.Purpose.Value,
        item.ConsentBasis,
        item.RetainUntil,
        item.Status.Value,
        item.Version,
        item.UpdatedAtUtc);
}
