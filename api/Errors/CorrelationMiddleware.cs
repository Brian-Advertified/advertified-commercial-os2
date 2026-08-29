namespace Advertified.Commercial.Api.Errors;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        context.TraceIdentifier = Guid.TryParse(incoming, out var parsed)
            ? parsed.ToString()
            : Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        await next(context);
    }
}
