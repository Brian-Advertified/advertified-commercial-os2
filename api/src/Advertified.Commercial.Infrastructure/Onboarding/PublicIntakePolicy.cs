using Advertified.Commercial.Application.Onboarding;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Onboarding;

internal sealed record NormalizedPublicIntake(
    string TypeCode,
    string Name,
    string Email,
    string? Phone,
    string Organisation,
    string? Website,
    string? Relationship,
    string? Message);

internal sealed record ProvisioningProfile(string TenantType, string Role, bool SupplierScoped);

internal static class PublicIntakePolicy
{
    internal static NormalizedPublicIntake Normalize(SubmitPublicIntakeRequest request)
    {
        var typeCode = Required(request.TypeCode, 50).ToUpperInvariant();
        if (!SupportedTypes.Contains(typeCode))
            throw new ArgumentException("Choose a supported enquiry or registration type.");
        var email = new EmailAddress(request.Email).Value;
        return new NormalizedPublicIntake(
            typeCode,
            Required(request.Name, 200),
            email,
            Optional(request.Phone, 50),
            Required(request.Organisation, 200),
            Website(request.Website),
            Optional(request.Relationship, 1_000),
            Optional(request.Message, 4_000));
    }

    internal static ProvisioningProfile ProvisioningFor(string typeCode) => typeCode switch
    {
        var code when code == MasterDataCodes.PublicIntakeTypes.Advertiser =>
            new(MasterDataCodes.TenantTypes.Advertiser, MasterDataCodes.Roles.AdvertiserAdmin, false),
        var code when code == MasterDataCodes.PublicIntakeTypes.Agency =>
            new(MasterDataCodes.TenantTypes.Agency, MasterDataCodes.Roles.AgencyAdmin, false),
        var code when code == MasterDataCodes.PublicIntakeTypes.MediaOwner =>
            new(MasterDataCodes.TenantTypes.Supplier, MasterDataCodes.Roles.SupplierUser, true),
        var code when code == MasterDataCodes.PublicIntakeTypes.Creator =>
            new(MasterDataCodes.TenantTypes.Creator, MasterDataCodes.Roles.InfluencerRep, true),
        _ => throw new InvalidOperationException(
            "Enquiries cannot be provisioned as organisation access."),
    };

    internal static string ReviewReason(string value) => Required(value, 1_000);

    internal static string LegalName(string value) => Required(value, 200);
    internal static string TradingName(string value) => Required(value, 200);
    internal static string? NormalizeWebsite(string? value) => Website(value);
    internal static string? NormalizeVatNumber(string? value) => Optional(value, 50);
    internal static bool IsEnquiry(string typeCode) =>
        typeCode is MasterDataCodes.PublicIntakeTypes.GeneralEnquiry or
            MasterDataCodes.PublicIntakeTypes.CampaignEnquiry;

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        MasterDataCodes.PublicIntakeTypes.GeneralEnquiry,
        MasterDataCodes.PublicIntakeTypes.CampaignEnquiry,
        MasterDataCodes.PublicIntakeTypes.Advertiser,
        MasterDataCodes.PublicIntakeTypes.Agency,
        MasterDataCodes.PublicIntakeTypes.MediaOwner,
        MasterDataCodes.PublicIntakeTypes.Creator,
    };

    private static string Required(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
            throw new ArgumentException("A required value is missing or too long.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximumLength)
            throw new ArgumentException("A supplied value is too long.");
        return normalized;
    }

    private static string? Website(string? value)
    {
        var normalized = Optional(value, 2_048);
        if (normalized is null) return null;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Use a valid public website address.");
        return uri.AbsoluteUri;
    }
}
