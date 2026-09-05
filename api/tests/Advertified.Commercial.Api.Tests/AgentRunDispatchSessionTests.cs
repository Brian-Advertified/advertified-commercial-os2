using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentRunDispatchSessionTests
{
    [Fact]
    public async Task StartupDrainsThenWaitsWithoutClaimsUntilWakeAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var firstWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wake = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = 3;
        var claims = 0;
        var deadlines = 0;
        var waits = 0;
        Task<bool> Claim(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            claims++;
            return Task.FromResult(queued-- > 0);
        }
        Task<DateTimeOffset?> Due(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            deadlines++;
            return Task.FromResult<DateTimeOffset?>(null);
        }
        async Task<bool> Wait(TimeSpan delay, CancellationToken token)
        {
            Assert.Equal(TimeSpan.FromSeconds(300), delay);
            if (++waits == 1)
            {
                firstWait.SetResult();
                return await wake.Task.WaitAsync(token);
            }
            secondWait.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return false;
        }

        var session = AgentRunDispatchSession.RunAsync(
            Claim, Due, Wait, TimeSpan.FromSeconds(300), TimeProvider.System, cancellation.Token);
        await firstWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, claims);
        Assert.Equal(1, deadlines);
        Assert.False(session.IsCompleted);
        queued = 2;
        wake.SetResult(true);
        await secondWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(7, claims);
        Assert.Equal(2, deadlines);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateSignalOrRecoverySweepAlwaysRechecksAuthoritativeClaim(bool signal)
    {
        using var cancellation = new CancellationTokenSource();
        var claims = 0;
        var waits = 0;
        Task<bool> Claim(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            claims++;
            return Task.FromResult(false);
        }
        Task<bool> Wait(TimeSpan delay, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(TimeSpan.FromSeconds(300), delay);
            if (++waits == 2) cancellation.Cancel();
            return Task.FromResult(signal);
        }
        await AgentRunDispatchSession.RunAsync(Claim,
            _ => Task.FromResult<DateTimeOffset?>(null), Wait,
            TimeSpan.FromSeconds(300), TimeProvider.System, cancellation.Token);
        Assert.Equal(2, claims);
    }

    [Theory]
    [InlineData(null, 300.0)]
    [InlineData(-30.0, 1.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(30.0, 30.0)]
    [InlineData(120.0, 120.0)]
    [InlineData(600.0, 300.0)]
    public void RetryAndLeaseDeadlinesBoundTheRecoverySweep(double? seconds, double expected)
    {
        var now = DateTimeOffset.UnixEpoch;
        var due = seconds.HasValue ? now.AddSeconds(seconds.Value) : (DateTimeOffset?)null;
        Assert.Equal(TimeSpan.FromSeconds(expected), AgentRunDispatchSession.WaitInterval(
            due, now, TimeSpan.FromSeconds(300)));
    }

    [Theory]
    [InlineData(300, 5, 60, true)]
    [InlineData(1, 5, 60, false)]
    [InlineData(300, 0, 60, false)]
    [InlineData(300, 60, 5, false)]
    [InlineData(300, 5, 301, false)]
    public void TimingRejectsRapidRecoveryAndUnboundedReconnect(
        int sweep, int minimum, int maximum, bool expected)
    {
        Assert.Equal(expected, AgentRuntimeOptions.HasSafeTiming(new AgentRuntimeOptions
        {
            RecoverySweepSeconds = sweep,
            ReconnectMinSeconds = minimum,
            ReconnectMaxSeconds = maximum,
        }));
    }
}
