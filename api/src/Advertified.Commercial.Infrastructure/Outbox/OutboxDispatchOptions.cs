namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed class OutboxDispatchOptions
{
    public const string SectionName = "OutboxDispatch";
    public const string DisabledMode = "Disabled";
    public const string DeterministicMode = "Deterministic";

    public string Mode { get; init; } = DisabledMode;

    public Guid? TenantId { get; init; }

    public int PollMilliseconds { get; init; } = 250;

    public int LeaseSeconds { get; init; } = 60;

    public int HeartbeatSeconds { get; init; } = 20;

    public int PublishTimeoutSeconds { get; init; } = 30;

    public bool DeterministicTransportAvailable { get; init; } = true;

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollMilliseconds);

    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatSeconds);

    public TimeSpan PublishTimeout => TimeSpan.FromSeconds(PublishTimeoutSeconds);

    public bool IsEnabled => Mode != DisabledMode;

    public static bool HasSupportedMode(OutboxDispatchOptions options) =>
        options.Mode is DisabledMode or DeterministicMode;

    public static bool HasRequiredTenant(OutboxDispatchOptions options) =>
        !options.IsEnabled ||
        options.TenantId is { } tenantId && tenantId != Guid.Empty;

    public static bool HasSupportedTiming(OutboxDispatchOptions options) =>
        options.PollMilliseconds is >= 25 and <= 5_000 &&
        options.LeaseSeconds is >= 5 and <= 300 &&
        options.HeartbeatSeconds is >= 1 and <= 149 &&
        options.HeartbeatSeconds <= (options.LeaseSeconds - 1) / 2 &&
        options.PublishTimeoutSeconds is >= 1 and <= 120;
}
