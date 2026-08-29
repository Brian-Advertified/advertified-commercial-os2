using System.Globalization;
using System.Security.Claims;
using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Endpoints;

public static class BrowserSessionEndpoints
{
    public static IEndpointRouteBuilder MapBrowserSessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/session").WithTags("Browser session");
        group.MapGet("", GetStatus)
            .WithName("GetBrowserSession")
            .AllowAnonymous()
            .Produces<BrowserSessionView>();
        group.MapPost("", StartAsync)
            .WithName("StartLocalBrowserSession")
            .AllowAnonymous()
            .Produces<BrowserSessionView>()
            .WithBrowserProblems();
        group.MapDelete("", EndAsync)
            .WithName("EndBrowserSession")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .WithBrowserProblems();
        return endpoints;
    }

    private static BrowserSessionView GetStatus(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var authenticated = context.User.Identity?.IsAuthenticated == true;
        return new BrowserSessionView(
            authenticated,
            RequireRequestToken(tokens),
            authenticated ? ReadExpiry(context.User) : null);
    }

    private static async Task<IResult> StartAsync(
        HttpContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        BrowserRequestGuard requestGuard,
        IBrowserSessionStore sessionStore,
        IAntiforgery antiforgery,
        IOptions<BrowserSessionOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        EnsureLocalMode(configuration, environment);
        await requestGuard.ValidateAsync(context);
        await InvalidateCurrentAsync(context, sessionStore, options.Value, cancellationToken);

        var identity = ReadConfiguredIdentity(configuration, options.Value, timeProvider);
        var session = await sessionStore.CreateAsync(identity, cancellationToken);
        context.Response.Cookies.Append(
            options.Value.CookieName,
            session.Token,
            CreateCookieOptions(options.Value, identity.ExpiresAtUtc));
        var requestToken = RequireRequestToken(antiforgery.GetAndStoreTokens(context));
        return Results.Ok(new BrowserSessionView(true, requestToken, identity.ExpiresAtUtc));
    }

    private static async Task<IResult> EndAsync(
        HttpContext context,
        IBrowserSessionStore sessionStore,
        IOptions<BrowserSessionOptions> options,
        CancellationToken cancellationToken)
    {
        await InvalidateCurrentAsync(context, sessionStore, options.Value, cancellationToken);
        context.Response.Cookies.Delete(
            options.Value.CookieName,
            CreateCookieOptions(options.Value, expiresAtUtc: null));
        return Results.NoContent();
    }

    private static BrowserSessionIdentity ReadConfiguredIdentity(
        IConfiguration configuration,
        BrowserSessionOptions options,
        TimeProvider timeProvider)
    {
        var userId = RequireIdentifier(configuration, "UserId");
        var actorId = RequireIdentifier(configuration, "ActorId");
        var identityType = configuration["Authentication:DevelopmentIdentity:IdentityType"]
            ?? LocalIdentityDefaults.HumanIdentity;
        if (!string.Equals(identityType, LocalIdentityDefaults.HumanIdentity, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Interactive session access denied.");
        }

        var expiry = timeProvider.GetUtcNow().AddMinutes(options.LifetimeMinutes);
        return new BrowserSessionIdentity(new UserId(userId), new ActorId(actorId), false, expiry);
    }

    private static Guid RequireIdentifier(IConfiguration configuration, string name)
    {
        var key = $"Authentication:DevelopmentIdentity:{name}";
        return Guid.TryParse(configuration[key], out var identifier)
            ? identifier
            : throw new InvalidOperationException("The local browser identity is not configured.");
    }

    private static void EnsureLocalMode(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var enabled = string.Equals(
                configuration["Authentication:Mode"],
                LocalIdentityDefaults.DeterministicSessionMode,
                StringComparison.Ordinal)
            && (environment.IsDevelopment() || environment.IsEnvironment("Test"));
        if (!enabled)
        {
            throw new UnauthorizedAccessException("Local browser sessions are disabled.");
        }
    }

    private static async Task InvalidateCurrentAsync(
        HttpContext context,
        IBrowserSessionStore sessionStore,
        BrowserSessionOptions options,
        CancellationToken cancellationToken)
    {
        if (context.Request.Cookies.TryGetValue(options.CookieName, out var currentToken))
        {
            await sessionStore.InvalidateAsync(currentToken, cancellationToken);
        }
    }

    private static CookieOptions CreateCookieOptions(
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

    private static DateTimeOffset? ReadExpiry(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(
            BrowserSessionAuthenticationHandler.ExpiryClaim);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiry)
            ? expiry
            : null;
    }

    private static string RequireRequestToken(AntiforgeryTokenSet tokens) =>
        tokens.RequestToken
        ?? throw new InvalidOperationException("The request token could not be created.");

    private static RouteHandlerBuilder WithBrowserProblems(this RouteHandlerBuilder builder) =>
        builder
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status400BadRequest,
                "application/problem+json")
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status403Forbidden,
                "application/problem+json");
}

public sealed record BrowserSessionView(
    bool Authenticated,
    string AntiforgeryToken,
    DateTimeOffset? ExpiresAtUtc);
