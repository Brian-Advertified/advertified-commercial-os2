namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class LocationDiscoveryOptions
{
    public const string SectionName = "LocationIntelligence:Discovery";
    public const string PublicServicePolicy = "https://operations.osmfoundation.org/policies/nominatim/";

    public string Endpoint { get; init; } = "https://nominatim.openstreetmap.org/";
    public string UserAgent { get; init; } = string.Empty;
    public bool PublicServicePolicyAccepted { get; init; }

    public void Validate()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(UserAgent) ||
            UserAgent.Contains('\r') || UserAgent.Contains('\n') ||
            uri.Host == "nominatim.openstreetmap.org" && !PublicServicePolicyAccepted)
        {
            throw new InvalidOperationException(
                "Location Intelligence discovery is not configured with a safe provider boundary.");
        }
    }
}
