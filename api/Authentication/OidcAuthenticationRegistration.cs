using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Advertified.Commercial.Api.Authentication;

internal static class OidcAuthenticationRegistration
{
    internal const string OidcScheme = "AdvertifiedOidc";
    private const string TransientScheme = "AdvertifiedOidcTransient";

    internal static void AddAdvertifiedOidc(
        this WebApplicationBuilder builder,
        AuthenticationBuilder authentication,
        string? authenticationMode)
    {
        var section = builder.Configuration.GetSection(OidcAuthenticationOptions.SectionName);
        builder.Services.AddOptions<OidcAuthenticationOptions>().Bind(section);
        if (!string.Equals(
                authenticationMode,
                LocalIdentityDefaults.OidcMode,
                StringComparison.Ordinal))
        {
            return;
        }

        var settings = section.Get<OidcAuthenticationOptions>()
            ?? new OidcAuthenticationOptions();
        if (!OidcAuthenticationOptions.HasSafeConfiguration(settings))
        {
            throw new InvalidOperationException(
                "Production OIDC authentication is not configured safely.");
        }

        builder.Services.AddOptions<OidcAuthenticationOptions>()
            .Bind(section)
            .Validate(OidcAuthenticationOptions.HasSafeConfiguration,
                "Production OIDC authentication is not configured safely.")
            .ValidateOnStart();
        builder.Services.AddScoped<AdvertifiedOidcEvents>();

        authentication.AddCookie(TransientScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });
        authentication.AddOpenIdConnect(OidcScheme, options =>
        {
            options.SignInScheme = TransientScheme;
            options.Authority = settings.Authority;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.CallbackPath = settings.CallbackPath;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.RequireHttpsMetadata = true;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.MapInboundClaims = false;
            options.EventsType = typeof(AdvertifiedOidcEvents);
            options.Scope.Clear();
            foreach (var scope in settings.Scopes)
            {
                options.Scope.Add(scope);
            }
        });
    }
}
