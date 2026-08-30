namespace Advertified.Commercial.Api.Authentication;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store, max-age=0";
                headers.Pragma = "no-cache";
                headers.Expires = "0";
            }
            return Task.CompletedTask;
        });
        await next(context);
    }
}
