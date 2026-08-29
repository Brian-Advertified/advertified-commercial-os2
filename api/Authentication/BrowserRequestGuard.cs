using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Authentication;

public sealed class BrowserRequestGuard(
    IAntiforgery antiforgery,
    IOptions<BrowserSessionOptions> options)
{
    public async Task ValidateAsync(HttpContext context)
    {
        EnsureAllowedOrigin(context.Request);
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException exception)
        {
            throw new BrowserAntiforgeryException(exception);
        }
    }

    private void EnsureAllowedOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            throw new BrowserOriginException();
        }

        var normalizedOrigin = originUri.GetLeftPart(UriPartial.Authority);
        var requestOrigin = $"{request.Scheme}://{request.Host}";
        var isAllowed = string.Equals(
                normalizedOrigin,
                requestOrigin,
                StringComparison.OrdinalIgnoreCase)
            || options.Value.AllowedOrigins.Contains(
                normalizedOrigin,
                StringComparer.OrdinalIgnoreCase);
        if (!isAllowed)
        {
            throw new BrowserOriginException();
        }
    }
}

public sealed class BrowserOriginException : Exception
{
    public BrowserOriginException()
        : base("The browser request origin was not allowed.")
    {
    }
}

public sealed class BrowserAntiforgeryException(Exception innerException)
    : Exception("The browser request token was invalid.", innerException);
