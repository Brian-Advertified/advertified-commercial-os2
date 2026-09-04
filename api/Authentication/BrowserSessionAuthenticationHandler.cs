using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Authentication;

public sealed class BrowserSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> authenticationOptions,
    IOptions<BrowserSessionOptions> sessionOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IBrowserSessionStore sessionStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        authenticationOptions,
        logger,
        encoder)
{
    public const string AuthenticationScheme = "AdvertifiedBrowserSession";
    public const string ExpiryClaim = "advertified:session_expires_at";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!IsEnabled())
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.Cookies.TryGetValue(sessionOptions.Value.CookieName, out var token))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await sessionStore.ResolveAsync(token, Context.RequestAborted);
        return session is null
            ? AuthenticateResult.Fail("The browser session is invalid or expired.")
            : AuthenticateResult.Success(CreateTicket(session));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        return WriteProblemAsync(
            StatusCodes.Status401Unauthorized,
            "Sign in required",
            "Sign in to continue.",
            "AUTHENTICATION_REQUIRED");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        return WriteProblemAsync(
            StatusCodes.Status403Forbidden,
            "Access denied",
            "You do not have access to this workspace or action.",
            "TENANT_FORBIDDEN");
    }

    private bool IsEnabled()
    {
        var mode = configuration["Authentication:Mode"];
        if (string.Equals(mode, LocalIdentityDefaults.OidcMode, StringComparison.Ordinal))
        {
            return true;
        }
        return string.Equals(
                mode,
                LocalIdentityDefaults.DeterministicSessionMode,
                StringComparison.Ordinal) &&
            (environment.IsDevelopment() || environment.IsEnvironment("Test"));
    }

    private static AuthenticationTicket CreateTicket(BrowserSessionIdentity session)
    {
        var identityType = session.IsServiceIdentity
            ? LocalIdentityDefaults.ServiceIdentity
            : LocalIdentityDefaults.HumanIdentity;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.Value.ToString()),
            new Claim("advertified:actor_id", session.ActorId.Value.ToString()),
            new Claim("advertified:identity_type", identityType),
            new Claim(
                ExpiryClaim,
                session.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
        return new AuthenticationTicket(principal, AuthenticationScheme);
    }

    private Task WriteProblemAsync(int status, string title, string detail, string code)
    {
        Response.StatusCode = status;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(
            new HumanSafeProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = ProblemTypeReference.Create(code),
                Code = code,
                CorrelationId = Context.TraceIdentifier,
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }
}
