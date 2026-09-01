namespace Advertified.Commercial.Api.Authentication;

internal static class BrowserSessionCookie
{
    internal static void Append(
        HttpResponse response,
        BrowserSessionOptions options,
        string token,
        DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(
            options.CookieName,
            token,
            CreateOptions(options, expiresAtUtc));
    }

    internal static void Delete(HttpResponse response, BrowserSessionOptions options)
    {
        response.Cookies.Delete(
            options.CookieName,
            CreateOptions(options, expiresAtUtc: null));
    }

    private static CookieOptions CreateOptions(
        BrowserSessionOptions options,
        DateTimeOffset? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = options.SecureCookie,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        Expires = expiresAtUtc,
    };
}
