using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Advertified.Commercial.Api.Observability;

public sealed partial class RequestCompletionTelemetryMiddleware(
    RequestDelegate next,
    ILogger<RequestCompletionTelemetryMiddleware> logger)
{
    private const string CorrelationTagName = "advertified.correlation_id";
    private const string UnmatchedRoute = "unmatched";
    private const string OtherMethod = "_OTHER";

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var activity = Activity.Current;
        activity?.SetTag(CorrelationTagName, context.TraceIdentifier);
        var escaped = false;

        try
        {
            await next(context);
        }
        catch
        {
            escaped = true;
            throw;
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var method = NormalizeMethod(context.Request.Method);
                var routeTemplate = GetRouteTemplate(context);
                var traceId = activity?.TraceId.ToString() ?? "unavailable";
                LogRequestCompleted(
                    logger,
                    method,
                    routeTemplate,
                    escaped ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                    double.IsFinite(duration) && duration >= 0 ? duration : 0,
                    context.TraceIdentifier,
                    traceId);
            }
        }
    }

    private static string GetRouteTemplate(HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint endpoint &&
        !string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText)
            ? endpoint.RoutePattern.RawText
            : UnmatchedRoute;

    private static string NormalizeMethod(string method) => method switch
    {
        "GET" => "GET",
        "POST" => "POST",
        "PUT" => "PUT",
        "PATCH" => "PATCH",
        "DELETE" => "DELETE",
        "HEAD" => "HEAD",
        "OPTIONS" => "OPTIONS",
        "CONNECT" => "CONNECT",
        "TRACE" => "TRACE",
        _ => OtherMethod,
    };

    [LoggerMessage(
        EventId = 12_003,
        Level = LogLevel.Information,
        Message = "HTTP request completed. Method={Method} RouteTemplate={RouteTemplate} " +
            "StatusCode={StatusCode} DurationMilliseconds={DurationMilliseconds} " +
            "CorrelationId={CorrelationId} TraceId={TraceId}")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string routeTemplate,
        int statusCode,
        double durationMilliseconds,
        string correlationId,
        string traceId);
}
