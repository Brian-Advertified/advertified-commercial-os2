namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class PlaceDiscoveryOptions
{
    public const string SectionName = "PlaceDiscovery";
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = "https://nominatim.openstreetmap.org/";
    public string UserAgent { get; init; } = string.Empty;
    public bool PublicServicePolicyAccepted { get; init; }
    public const string PublicServicePolicy = "https://operations.osmfoundation.org/policies/nominatim/";

    public bool IsConfigured() => Enabled &&
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) && !string.IsNullOrWhiteSpace(UserAgent) &&
        !UserAgent.Contains('\r') && !UserAgent.Contains('\n') &&
        (uri.Host != "nominatim.openstreetmap.org" || PublicServicePolicyAccepted);
}
