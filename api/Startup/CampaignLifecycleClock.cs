using System.Globalization;
using Advertified.Commercial.Application.Campaign;

namespace Advertified.Commercial.Api.Startup;

internal sealed class CampaignLifecycleClock(
    TimeProvider systemTime,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ICampaignLifecycleClock
{
    internal const string OverrideKey = "Certification:CampaignUtcNow";

    public DateTimeOffset GetUtcNow()
    {
        var configured = configuration[OverrideKey];
        if (string.IsNullOrWhiteSpace(configured)) return systemTime.GetUtcNow();
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
            throw new InvalidOperationException("Campaign certification time is restricted to development and test.");
        return DateTimeOffset.TryParse(
            configured,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
            ? value.ToUniversalTime()
            : throw new InvalidOperationException("Campaign certification time must be a valid ISO-8601 timestamp.");
    }
}
