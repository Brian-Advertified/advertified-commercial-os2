using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OutboxDeliveryContractTests
{
    [Fact]
    public void FailureCodesUseTheDatabaseSafePortableTokenShape()
    {
        var invalidCodes = new[]
        {
            "_LEADING_UNDERSCORE",
            "-LEADING_HYPHEN",
            ".LEADING_DOT",
            ":LEADING_COLON",
            "CONTAINS SPACE",
        };

        foreach (var failureCode in invalidCodes)
        {
            Assert.Throws<ArgumentException>(() =>
                OutboxPublishResult.TransientFailure(failureCode));
        }

        var result = OutboxPublishResult.TerminalFailure("VALID_FAILURE.CODE-1");
        Assert.Equal("VALID_FAILURE.CODE-1", result.FailureCode);
    }

    [Fact]
    public void TimingValidationRejectsOverflowAndHalfLeaseHeartbeats()
    {
        Assert.False(OutboxDispatchOptions.HasSupportedTiming(new OutboxDispatchOptions
        {
            LeaseSeconds = 60,
            HeartbeatSeconds = int.MaxValue,
        }));
        Assert.True(OutboxDispatchOptions.HasSupportedTiming(new OutboxDispatchOptions
        {
            LeaseSeconds = 5,
            HeartbeatSeconds = 2,
        }));
        Assert.False(OutboxDispatchOptions.HasSupportedTiming(new OutboxDispatchOptions
        {
            LeaseSeconds = 5,
            HeartbeatSeconds = 3,
        }));
    }
}
