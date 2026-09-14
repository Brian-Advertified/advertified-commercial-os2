using System.Globalization;

namespace Advertified.Commercial.Api.Startup;

/// <summary>
/// Uses the system clock normally. In Development/Test only, the existing campaign
/// certification override can move API wall-clock time forward so delivery and
/// measurement certification obey the same temporal rules as campaign lifecycle.
/// </summary>
internal sealed class CertificationTimeProvider(
    IConfiguration configuration,
    IWebHostEnvironment environment) : TimeProvider
{
    private readonly TimeProvider system = TimeProvider.System;

    public override DateTimeOffset GetUtcNow()
    {
        var configured = configuration[CampaignLifecycleClock.OverrideKey];
        if (string.IsNullOrWhiteSpace(configured)) return system.GetUtcNow();
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
            throw new InvalidOperationException(
                "Campaign certification time is restricted to development and test.");
        return DateTimeOffset.TryParse(
            configured,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
            ? value.ToUniversalTime()
            : throw new InvalidOperationException(
                "Campaign certification time must be a valid ISO-8601 timestamp.");
    }

    public override TimeZoneInfo LocalTimeZone => system.LocalTimeZone;
    public override long TimestampFrequency => system.TimestampFrequency;
    public override long GetTimestamp() => system.GetTimestamp();
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) => system.CreateTimer(callback, state, dueTime, period);
}
