namespace Advertified.Commercial.Api.Authentication;

public sealed class BrowserSessionOptions
{
    public const string SectionName = "Authentication:BrowserSession";
    public const string DefaultCookieName = "advertified.session";
    public const string DefaultAntiforgeryCookieName = "advertified.antiforgery";
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
    public const int DefaultLifetimeMinutes = 480;

    public string CookieName { get; init; } = DefaultCookieName;

    public string AntiforgeryCookieName { get; init; } = DefaultAntiforgeryCookieName;

    public int LifetimeMinutes { get; init; } = DefaultLifetimeMinutes;

    public bool SecureCookie { get; init; } = true;

    public string[] AllowedOrigins { get; init; } = [];
}
