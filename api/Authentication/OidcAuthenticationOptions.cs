namespace Advertified.Commercial.Api.Authentication;

public sealed class OidcAuthenticationOptions
{
    public const string SectionName = "Authentication:Oidc";
    public const string DefaultProviderCode = "cognito";
    public const string DefaultCallbackPath = "/api/v1/session/oidc-callback";
    public const string DefaultSubjectClaim = "sub";
    public const string DefaultEmailClaim = "email";
    public const string DefaultEmailVerifiedClaim = "email_verified";
    public const string DefaultMfaClaim = "amr";
    public const string DefaultMfaValue = "mfa";

    public string ProviderCode { get; init; } = DefaultProviderCode;
    public string? Authority { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? LogoutEndpoint { get; init; }
    public string? PostLogoutRedirectUri { get; init; }
    public string CallbackPath { get; init; } = DefaultCallbackPath;
    public string SubjectClaim { get; init; } = DefaultSubjectClaim;
    public string EmailClaim { get; init; } = DefaultEmailClaim;
    public string EmailVerifiedClaim { get; init; } = DefaultEmailVerifiedClaim;
    public string MfaClaim { get; init; } = DefaultMfaClaim;
    public string MfaValue { get; init; } = DefaultMfaValue;
    public string[] Scopes { get; init; } = ["openid", "email", "profile"];

    public static bool HasSafeConfiguration(OidcAuthenticationOptions options)
    {
        return IsHttps(options.Authority) &&
            IsHttps(options.LogoutEndpoint) &&
            IsHttps(options.PostLogoutRedirectUri) &&
            !string.IsNullOrWhiteSpace(options.ClientId) &&
            !string.IsNullOrWhiteSpace(options.ClientSecret) &&
            IsSafeCode(options.ProviderCode, 50) &&
            IsSafePath(options.CallbackPath) &&
            IsSafeCode(options.SubjectClaim, 100) &&
            IsSafeCode(options.EmailClaim, 100) &&
            IsSafeCode(options.EmailVerifiedClaim, 100) &&
            IsSafeCode(options.MfaClaim, 100) &&
            !string.IsNullOrWhiteSpace(options.MfaValue) &&
            options.MfaValue.Length <= 100 &&
            options.Scopes.Length is >= 1 and <= 10 &&
            options.Scopes.Contains("openid", StringComparer.Ordinal) &&
            options.Scopes.All(scope => IsSafeCode(scope, 100));
    }

    private static bool IsHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsSafePath(string value) =>
        value.StartsWith('/', StringComparison.Ordinal) &&
        !value.StartsWith("//", StringComparison.Ordinal) &&
        !value.Contains('\\');

    private static bool IsSafeCode(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-' or ':');
}
