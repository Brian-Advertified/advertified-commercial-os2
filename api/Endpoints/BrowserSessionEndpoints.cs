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
            .RequireRateLimiting(RequestRateLimitPolicies.BrowserSessionStatus)
            .Produces<BrowserSessionView>();
        group.MapGet("/login", BeginOidcAsync)
            .WithName("BeginOidcBrowserSession")
            .AllowAnonymous()
            .RequireRateLimiting(RequestRateLimitPolicies.BrowserSession);
        group.MapPost("/logout", EndOidcAsync)
            .WithName("EndOidcBrowserSession")
            .AllowAnonymous()
            .RequireRateLimiting(RequestRateLimitPolicies.BrowserSession)
            .Produces<OidcSignOutView>()
            .WithBrowserProblems();
        group.MapPost("", StartAsync)
            .WithName("StartLocalBrowserSession")
            .AllowAnonymous()
            .RequireRateLimiting(RequestRateLimitPolicies.BrowserSession)
            .Produces<BrowserSessionView>()
            .WithBrowserProblems();
        group.MapDelete("", EndAsync)
            .WithName("EndBrowserSession")
            .AllowAnonymous()
            .RequireRateLimiting(RequestRateLimitPolicies.BrowserSession)
            .Produces(StatusCodes.Status204NoContent)
            .WithBrowserProblems();
        return endpoints;
    }

    private static BrowserSessionView GetStatus(
        HttpContext context,
        IConfiguration configuration,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var authenticated = context.User.Identity?.IsAuthenticated == true;
        var oidc = string.Equals(
            configuration["Authentication:Mode"],
            LocalIdentityDefaults.OidcMode,
            StringComparison.Ordinal);
        return new BrowserSessionView(
            authenticated,
            RequireRequestToken(tokens),
            authenticated ? ReadExpiry(context.User) : null,
            oidc ? "/api/v1/session/login" : null,
            oidc ? "/api/v1/session/logout" : null);
    }

    private static IResult BeginOidcAsync(
        string? returnTo,
        IConfiguration configuration)
    {
        EnsureOidcMode(configuration);
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = BrowserReturnPath.Normalize(returnTo),
        };
        return Results.Challenge(
            properties,
            [OidcAuthenticationRegistration.OidcScheme]);
    }

    private static async Task<IResult> EndOidcAsync(
        HttpContext context,
        IConfiguration configuration,
        BrowserRequestGuard requestGuard,
        IBrowserSessionStore sessionStore,
        IOptions<BrowserSessionOptions> sessionOptions,
        IOptions<OidcAuthenticationOptions> oidcOptions,
        CancellationToken cancellationToken)
    {
        EnsureOidcMode(configuration);
        await requestGuard.ValidateAsync(context);
        await InvalidateCurrentAsync(
            context, sessionStore, sessionOptions.Value, cancellationToken);
        BrowserSessionCookie.Delete(context.Response, sessionOptions.Value);
        return Results.Ok(new OidcSignOutView(
            BuildOidcLogoutUrl(oidcOptions.Value)));
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
        BrowserSessionCookie.Append(
            context.Response,
            options.Value,
            session.Token,
            identity.ExpiresAtUtc);
        var requestToken = RequireRequestToken(antiforgery.GetAndStoreTokens(context));
        return Results.Ok(new BrowserSessionView(
            true,
            requestToken,
            identity.ExpiresAtUtc,
            null,
            null));
    }

    private static async Task<IResult> EndAsync(
        HttpContext context,
        IBrowserSessionStore sessionStore,
        IOptions<BrowserSessionOptions> options,
        CancellationToken cancellationToken)
    {
        await InvalidateCurrentAsync(context, sessionStore, options.Value, cancellationToken);
        BrowserSessionCookie.Delete(context.Response, options.Value);
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

    private static void EnsureOidcMode(IConfiguration configuration)
    {
        if (!string.Equals(
                configuration["Authentication:Mode"],
                LocalIdentityDefaults.OidcMode,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Managed sign in is unavailable.");
        }
    }

    private static string BuildOidcLogoutUrl(OidcAuthenticationOptions options)
    {
        if (!OidcAuthenticationOptions.HasSafeConfiguration(options))
        {
            throw new InvalidOperationException("Managed sign out is not configured safely.");
        }
        var separator = options.LogoutEndpoint!.Contains('?') ? "&" : "?";
        return string.Concat(
            options.LogoutEndpoint,
            separator,
            "client_id=",
            Uri.EscapeDataString(options.ClientId!),
            "&logout_uri=",
            Uri.EscapeDataString(options.PostLogoutRedirectUri!));
    }

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
    DateTimeOffset? ExpiresAtUtc,
    string? SignInPath,
    string? SignOutPath);

public sealed record OidcSignOutView(string RedirectUrl);
