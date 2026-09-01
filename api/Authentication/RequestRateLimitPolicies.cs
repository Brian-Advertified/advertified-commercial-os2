using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Advertified.Commercial.Api.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Advertified.Commercial.Api.Authentication;

public static class RequestRateLimitPolicies
{
    public const string BrowserSession = "browser-session";
    public const string BrowserSessionStatus = "browser-session-status";
    public const string ProviderCallback = "provider-callback";
    public const string InventoryUpload = "inventory-upload";
    public const string AgentWork = "agent-work";
    public const string HeavyWork = "heavy-work";

    private const int BusinessMutationPermitLimit = 60;
    private const int AgentWorkPermitLimit = 12;
    private const int HeavyWorkPermitLimit = 20;
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions ProblemJson =
        new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddAdvertifiedRateLimits(
        this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsSafeMethod(context.Request.Method)
                    ? RateLimitPartition.GetNoLimiter("safe-request")
                    : FixedWindow(
                        "business:" + ActorTenant(context),
                        BusinessMutationPermitLimit,
                        OneMinute));
            options.AddPolicy(BrowserSession, context => FixedWindow(
                "session:" + RemoteAddress(context), 20, OneMinute));
            options.AddPolicy(BrowserSessionStatus, context => FixedWindow(
                "session-status:" + RemoteAddress(context), 60, OneMinute));
            options.AddPolicy(ProviderCallback, context => FixedWindow(
                "provider:" + RemoteAddress(context), 120, OneMinute));
            options.AddPolicy(InventoryUpload, context => FixedWindow(
                "inventory:" + ActorTenant(context), 30, TenMinutes));
            options.AddPolicy(AgentWork, context => FixedWindow(
                "agent:" + ActorTenant(context), AgentWorkPermitLimit, OneMinute));
            options.AddPolicy(HeavyWork, context => FixedWindow(
                "heavy:" + ActorTenant(context), HeavyWorkPermitLimit, FiveMinutes));
        });

    private static RateLimitPartition<string> FixedWindow(
        string key,
        int permitLimit,
        TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = window,
        });

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }
        var problem = new HumanSafeProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Too many requests were received. Try again shortly.",
            Code = "RATE_LIMITED",
            CorrelationId = context.HttpContext.TraceIdentifier,
        };
        await JsonSerializer.SerializeAsync(
            response.Body, problem, ProblemJson, cancellationToken);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static string ActorTenant(HttpContext context)
    {
        var tenant = context.Request.RouteValues.TryGetValue("tenantId", out var value)
            ? value?.ToString() ?? "none"
            : "none";
        return tenant + ':' + Actor(context);
    }

    private static string Actor(HttpContext context) =>
        context.User.FindFirstValue("advertified:actor_id")
        ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? RemoteAddress(context);

    private static string RemoteAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
