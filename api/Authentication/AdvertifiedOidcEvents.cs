using System.Globalization;
using System.Security.Claims;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Authentication;

public sealed partial class AdvertifiedOidcEvents(
    OidcIdentityResolver identityResolver,
    IBrowserSessionStore sessionStore,
    IOptions<BrowserSessionOptions> sessionOptions,
    IOptions<OidcAuthenticationOptions> oidcOptions,
    TimeProvider timeProvider,
    ILogger<AdvertifiedOidcEvents> logger) : OpenIdConnectEvents
{
    public override async Task TicketReceived(TicketReceivedContext context)
    {
        var principal = context.Principal
            ?? throw new UnauthorizedAccessException("Identity access denied.");
        var settings = oidcOptions.Value;
        var subject = RequiredClaim(principal, settings.SubjectClaim);
        var email = RequiredClaim(principal, settings.EmailClaim);
        var emailVerified = ReadBooleanClaim(principal, settings.EmailVerifiedClaim);
        var identity = await identityResolver.ResolveAsync(
            settings.ProviderCode,
            subject,
            email,
            emailVerified,
            context.HttpContext.RequestAborted);
        if (identity.MfaRequired && !HasMfa(principal, settings))
        {
            throw new UnauthorizedAccessException("Multi-factor authentication is required.");
        }

        var expiresAt = ProviderExpiry(principal);
        var sessionLimit = timeProvider.GetUtcNow()
            .AddMinutes(sessionOptions.Value.LifetimeMinutes);
        if (expiresAt > sessionLimit)
        {
            expiresAt = sessionLimit;
        }
        if (expiresAt <= timeProvider.GetUtcNow())
        {
            throw new UnauthorizedAccessException("Identity access denied.");
        }

        var session = await sessionStore.CreateAsync(
            new BrowserSessionIdentity(
                identity.UserId,
                identity.ActorId,
                false,
                expiresAt),
            context.HttpContext.RequestAborted);
        BrowserSessionCookie.Append(
            context.Response,
            sessionOptions.Value,
            session.Token,
            session.Identity.ExpiresAtUtc);
        context.Response.Redirect(BrowserReturnPath.Normalize(context.Properties.RedirectUri));
        context.HandleResponse();
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        LogRemoteFailure(logger, context.HttpContext.TraceIdentifier);
        context.Response.Redirect("/sign-in?authentication=failed");
        context.HandleResponse();
        return Task.CompletedTask;
    }

    private static string RequiredClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new UnauthorizedAccessException("Identity access denied.")
            : value;
    }

    private static bool ReadBooleanClaim(ClaimsPrincipal principal, string claimType) =>
        bool.TryParse(principal.FindFirstValue(claimType), out var value) && value;

    private static bool HasMfa(
        ClaimsPrincipal principal,
        OidcAuthenticationOptions options)
    {
        return principal.FindAll(options.MfaClaim).Any(claim =>
            claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Contains(options.MfaValue, StringComparer.OrdinalIgnoreCase));
    }

    private static DateTimeOffset ProviderExpiry(ClaimsPrincipal principal)
    {
        var expiry = principal.FindFirstValue("exp");
        return long.TryParse(expiry, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : throw new UnauthorizedAccessException("Identity access denied.");
    }

    [LoggerMessage(
        EventId = 12_201,
        Level = LogLevel.Warning,
        Message = "OIDC authentication failed safely. Correlation {CorrelationId}")]
    private static partial void LogRemoteFailure(ILogger logger, string correlationId);
}
