using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Endpoints;

/// <summary>
/// Development-only support for connected browser certification. These endpoints are mapped
/// only by Program when ASPNETCORE_ENVIRONMENT is Development. They do not perform commercial
/// actions; they only provision deterministic local actors/workspace data and change the local
/// browser session identity so the UI can exercise the real role/tenant authorization boundary.
/// </summary>
public static class ConnectedAcceptanceDevelopmentEndpoints
{
    private const string FixturePath = "/app/development/seed-connected-acceptance.sql";

    public static IEndpointRouteBuilder MapConnectedAcceptanceDevelopmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/development/connected-acceptance")
            .WithTags("Development connected acceptance")
            .RequireAuthorization();

        group.MapPost("/bootstrap", BootstrapAsync)
            .WithName("BootstrapConnectedAcceptanceDevelopmentData");
        group.MapPost("/identity", SwitchIdentityAsync)
            .WithName("SwitchConnectedAcceptanceDevelopmentIdentity")
            .Produces<BrowserSessionView>();
        return endpoints;
    }

    private static async Task<IResult> BootstrapAsync(
        HttpContext context,
        BrowserRequestGuard requestGuard,
        GovernanceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await requestGuard.ValidateAsync(context);
        if (!File.Exists(FixturePath))
        {
            throw new InvalidOperationException(
                "The connected acceptance development fixture is not available.");
        }

        var sql = await File.ReadAllTextAsync(FixturePath, cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return Results.Ok(new
        {
            buyerTenantId = Guid.Parse("10000000-0000-0000-0000-000000000040"),
            supplierTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
        });
    }

    private static async Task<IResult> SwitchIdentityAsync(
        ConnectedAcceptanceIdentityCommand command,
        HttpContext context,
        BrowserRequestGuard requestGuard,
        GovernanceDbContext dbContext,
        IBrowserSessionStore sessionStore,
        IAntiforgery antiforgery,
        IOptions<BrowserSessionOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await requestGuard.ValidateAsync(context);

        var targetExists = false;
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var query = dbContext.Database.GetDbConnection().CreateCommand();
            query.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM commercial.users app_user
                    WHERE app_user.id = @user_id
                      AND app_user.status_code = 'ACTIVE'
                      AND lower(app_user.email) LIKE '%@advertified.local')
                """;
            var parameter = query.CreateParameter();
            parameter.ParameterName = "user_id";
            parameter.Value = command.UserId;
            query.Parameters.Add(parameter);
            targetExists = await query.ExecuteScalarAsync(cancellationToken) is true;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        if (!targetExists)
        {
            throw new UnauthorizedAccessException(
                "Connected acceptance identity access denied.");
        }

        if (context.Request.Cookies.TryGetValue(options.Value.CookieName, out var currentToken))
        {
            await sessionStore.InvalidateAsync(currentToken, cancellationToken);
        }

        var expiry = timeProvider.GetUtcNow().AddMinutes(options.Value.LifetimeMinutes);
        var identity = new BrowserSessionIdentity(
            new UserId(command.UserId),
            new ActorId(command.UserId),
            false,
            expiry);
        var session = await sessionStore.CreateAsync(identity, cancellationToken);
        BrowserSessionCookie.Append(
            context.Response,
            options.Value,
            session.Token,
            identity.ExpiresAtUtc);

        var tokens = antiforgery.GetAndStoreTokens(context);
        var requestToken = tokens.RequestToken
            ?? throw new InvalidOperationException("The request token could not be created.");
        return Results.Ok(new BrowserSessionView(
            true,
            requestToken,
            identity.ExpiresAtUtc,
            null,
            null));
    }
}

public sealed record ConnectedAcceptanceIdentityCommand(Guid UserId);
