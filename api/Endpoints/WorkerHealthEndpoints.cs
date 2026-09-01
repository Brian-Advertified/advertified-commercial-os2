using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Infrastructure.Worker;

namespace Advertified.Commercial.Api.Endpoints;

public static class WorkerHealthEndpoints
{
    private static readonly string[] ProcessChecks = ["process"];
    private static readonly string[] SchedulerChecks = ["process", "scheduler-database"];
    private static readonly string[] TransportChecks =
        ["process", "scheduler-database", "outbox-transport"];

    public static IEndpointRouteBuilder MapWorkerHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            service = "advertified-commercial-worker",
            checks = ProcessChecks,
        })).AllowAnonymous();
        endpoints.MapGet("/health/ready", ReadyAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> ReadyAsync(
        WorkerSchedulerStore scheduler,
        OutboxDispatchReadiness outbox,
        CancellationToken cancellationToken)
    {
        if (!await scheduler.CheckHealthAsync(cancellationToken))
        {
            return Results.Json(
                new
                {
                    status = "unavailable",
                    service = "advertified-commercial-worker",
                    checks = ProcessChecks,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (outbox.IsEnabled && !await outbox.IsReadyAsync(cancellationToken))
        {
            return Results.Json(
                new
                {
                    status = "unavailable",
                    service = "advertified-commercial-worker",
                    checks = SchedulerChecks,
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return Results.Ok(new
        {
            status = "ready",
            service = "advertified-commercial-worker",
            checks = outbox.IsEnabled ? TransportChecks : SchedulerChecks,
        });
    }
}
