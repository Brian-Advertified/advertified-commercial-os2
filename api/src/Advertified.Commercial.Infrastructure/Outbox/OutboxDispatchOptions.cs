namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed class OutboxDispatchOptions
{
    public const string SectionName = "OutboxDispatch";
    public const string DisabledMode = "Disabled";
    public const string DeterministicMode = "Deterministic";
    public const string EventBridgeMode = "EventBridge";

    public string Mode { get; init; } = DisabledMode;
    public Guid? TenantId { get; init; }
    public int PollMilliseconds { get; init; } = 250;
    public int LeaseSeconds { get; init; } = 60;
    public int HeartbeatSeconds { get; init; } = 20;
    public int PublishTimeoutSeconds { get; init; } = 30;
    public bool DeterministicTransportAvailable { get; init; } = true;
    public string? AwsRegion { get; init; }
    public string? EventBusName { get; init; }
    public string EventSource { get; init; } = "advertified.commercial";

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollMilliseconds);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatSeconds);
    public TimeSpan PublishTimeout => TimeSpan.FromSeconds(PublishTimeoutSeconds);
    public bool IsEnabled => Mode != DisabledMode;

    public static bool HasSupportedMode(OutboxDispatchOptions options) =>
        options.Mode is DisabledMode or DeterministicMode or EventBridgeMode;

    public static bool HasValidLocalTenant(OutboxDispatchOptions options) =>
        options.TenantId is null || options.TenantId != Guid.Empty;

    public static bool HasSafeTransportConfiguration(OutboxDispatchOptions options)
    {
        if (options.Mode != EventBridgeMode)
        {
            return true;
        }
        return IsSafeToken(options.AwsRegion, 50) &&
            IsSafeToken(options.EventBusName, 256) &&
            IsSafeSource(options.EventSource);
    }

    public static bool HasSupportedTiming(OutboxDispatchOptions options) =>
        options.PollMilliseconds is >= 25 and <= 5_000 &&
        options.LeaseSeconds is >= 5 and <= 300 &&
        options.HeartbeatSeconds is >= 1 and <= 149 &&
        options.HeartbeatSeconds <= (options.LeaseSeconds - 1) / 2 &&
        options.PublishTimeoutSeconds is >= 1 and <= 120;

    private static bool IsSafeToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.' or '/' or ':');

    private static bool IsSafeSource(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256 &&
        value.All(character => char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.');
}
