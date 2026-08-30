namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailAutomationOptions
{
    public const string SectionName = "EmailAutomation";
    public const string DisabledMode = "Disabled";
    public const string DeterministicMode = "Deterministic";
    public const string ResendMode = "Resend";

    public string Mode { get; init; } = DisabledMode;
    public string ResendApiBaseUrl { get; init; } = "https://api.resend.com/";
    public string? ResendApiKey { get; init; }
    public string? ResendWebhookSecret { get; init; }
    public string? SenderAddress { get; init; }
    public int WebhookToleranceSeconds { get; init; } = 300;
    public bool ProcessInline { get; init; } = true;

    public static bool IsSupported(EmailAutomationOptions options) =>
        options.Mode is DisabledMode or DeterministicMode or ResendMode;

    public static bool HasValidTolerance(EmailAutomationOptions options) =>
        options.WebhookToleranceSeconds is >= 30 and <= 900;

    public static bool HasProviderConfiguration(EmailAutomationOptions options) =>
        options.Mode != ResendMode ||
        Uri.TryCreate(options.ResendApiBaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(options.ResendApiKey) &&
        !string.IsNullOrWhiteSpace(options.ResendWebhookSecret) &&
        !string.IsNullOrWhiteSpace(options.SenderAddress);
}
