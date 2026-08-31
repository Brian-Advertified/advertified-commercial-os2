using System.Data.Common;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Api.Endpoints;

public static partial class HealthEndpoints
{
    private const string Service = "advertified-commercial-api";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", Live)
            .WithTags("Health")
            .Produces<HealthResponse>();
        endpoints.MapGet("/health/ready", ReadyAsync)
            .WithTags("Health")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    private static IResult Live() => Results.Ok(new HealthResponse(
        "healthy", Service, ["process"]));

    private static async Task<IResult> ReadyAsync(
        GovernanceDbContext database,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(HealthEndpoints));
        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                LogDatabaseUnavailable(logger);
                return Unavailable("database-unavailable");
            }

            if (!await database.MasterDataSets.AsNoTracking().AnyAsync(cancellationToken))
            {
                LogMasterDataUnavailable(logger);
                return Unavailable("master-data-unavailable", "database");
            }
        }
        catch (Exception exception) when (IsDependencyFailure(exception, cancellationToken))
        {
            LogDatabaseUnavailable(logger);
            return Unavailable("database-unavailable");
        }

        return Results.Ok(new HealthResponse(
            "ready", Service, ["process", "database", "master-data"]));
    }

    private static bool IsDependencyFailure(
        Exception exception,
        CancellationToken cancellationToken) =>
        exception is DbException
        or TimeoutException
        or InvalidOperationException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static IResult Unavailable(string failedCheck, params string[] passedChecks) =>
        Results.Json(
            new HealthResponse("unavailable", Service, ["process", .. passedChecks, failedCheck]),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    [LoggerMessage(
        EventId = 12_001,
        Level = LogLevel.Warning,
        Message = "Canonical database readiness failed.")]
    private static partial void LogDatabaseUnavailable(ILogger logger);

    [LoggerMessage(
        EventId = 12_002,
        Level = LogLevel.Warning,
        Message = "Governed master-data readiness failed.")]
    private static partial void LogMasterDataUnavailable(ILogger logger);
}
