using System.Security.Claims;
using System.Text.Encodings.Web;
using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Authentication;

public static class LocalIdentityDefaults
{
    public const string Scheme = "AdvertifiedLocalIdentity";
    public const string CompositeScheme = "AdvertifiedIdentity";
    public const string DisabledMode = "Disabled";
    public const string DeterministicMode = "Deterministic";
    public const string DeterministicSessionMode = "DeterministicSession";
    public const string OidcMode = "Oidc";
    public const string HumanIdentity = "human";
    public const string ServiceIdentity = "service";
}

public sealed class ClaimsCurrentIdentity(IHttpContextAccessor contextAccessor)
    : ICurrentIdentity
{
    public UserId UserId => new(ReadIdentifier(ClaimTypes.NameIdentifier));

    public ActorId ActorId => new(ReadIdentifier("advertified:actor_id"));

    public bool IsServiceIdentity =>
        string.Equals(
            contextAccessor.HttpContext?.User.FindFirstValue("advertified:identity_type"),
            LocalIdentityDefaults.ServiceIdentity,
            StringComparison.Ordinal);

    private Guid ReadIdentifier(string claimType)
    {
        var value = contextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var identifier)
            ? identifier
            : throw new UnauthorizedAccessException("Identity access denied.");
    }
}

public sealed class DevelopmentIdentityHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var mode = configuration["Authentication:Mode"];
        if (!string.Equals(mode, LocalIdentityDefaults.DeterministicMode, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Local identity is disabled."));
        }

        if (IsInvalidOrExpired())
        {
            return Task.FromResult(AuthenticateResult.Fail("Local identity has expired."));
        }

        var userId = RequireIdentifier("Authentication:DevelopmentIdentity:UserId");
        var actorId = RequireIdentifier("Authentication:DevelopmentIdentity:ActorId");
        var identityType = ReadIdentityType();
        if (identityType is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Local identity type is invalid."));
        }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("advertified:actor_id", actorId.ToString()),
            new Claim("advertified:identity_type", identityType),
        };
        var identity = new ClaimsIdentity(claims, LocalIdentityDefaults.Scheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            LocalIdentityDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return WriteProblemAsync(CreateProblem(
            StatusCodes.Status401Unauthorized,
            "Sign in required",
            "Sign in to continue.",
            "AUTHENTICATION_REQUIRED"));
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        return WriteProblemAsync(CreateProblem(
            StatusCodes.Status403Forbidden,
            "Access denied",
            "You do not have access to this workspace or action.",
            "TENANT_FORBIDDEN"));
    }

    private Task WriteProblemAsync(HumanSafeProblemDetails problem)
    {
        return Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }

    private HumanSafeProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string code)
    {
        return new HumanSafeProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = ProblemTypeReference.Create(code),
            Code = code,
            CorrelationId = Context.TraceIdentifier,
        };
    }

    private Guid RequireIdentifier(string key)
    {
        return Guid.TryParse(configuration[key], out var identifier)
            ? identifier
            : throw new InvalidOperationException(
                "The deterministic development identity is not configured.");
    }

    private string? ReadIdentityType()
    {
        var value = configuration["Authentication:DevelopmentIdentity:IdentityType"]
            ?? LocalIdentityDefaults.HumanIdentity;
        return value is LocalIdentityDefaults.HumanIdentity or LocalIdentityDefaults.ServiceIdentity
            ? value
            : null;
    }

    private bool IsInvalidOrExpired()
    {
        var value = configuration["Authentication:DevelopmentIdentity:ExpiresAtUtc"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !DateTimeOffset.TryParse(value, out var expiry)
            || expiry <= timeProvider.GetUtcNow();
    }
}
