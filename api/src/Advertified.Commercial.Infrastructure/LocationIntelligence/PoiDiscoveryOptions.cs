namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class PoiDiscoveryOptions
{
    public const string SectionName = "LocationIntelligence:PoiDiscovery";
    public const string PublicServicePolicy = "https://wiki.openstreetmap.org/wiki/Overpass_API";

    public string Endpoint { get; init; } = "https://overpass-api.de/api/interpreter";
    public string UserAgent { get; init; } = "Advertified-Commercial-OS/1.0";
    public bool PublicServicePolicyAccepted { get; init; }

    public void Validate()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(UserAgent) ||
            UserAgent.Contains('\r') || UserAgent.Contains('\n') ||
            uri.Host == "overpass-api.de" && !PublicServicePolicyAccepted)
        {
            throw new InvalidOperationException(
                "Location Intelligence POI discovery is not configured with a safe provider boundary.");
        }
    }
}
