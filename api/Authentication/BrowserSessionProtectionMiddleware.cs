using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Authentication;

public sealed class BrowserSessionProtectionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Get,
            HttpMethods.Head,
            HttpMethods.Options,
            HttpMethods.Trace,
        };

    public async Task InvokeAsync(
        HttpContext context,
        BrowserRequestGuard requestGuard,
        IOptions<BrowserSessionOptions> options)
    {
        var hasSessionCookie = context.Request.Cookies.ContainsKey(options.Value.CookieName);
        if (hasSessionCookie && !SafeMethods.Contains(context.Request.Method))
        {
            await requestGuard.ValidateAsync(context);
        }

        await next(context);
    }
}
