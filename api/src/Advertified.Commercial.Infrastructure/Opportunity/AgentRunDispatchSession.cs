namespace Advertified.Commercial.Infrastructure.Opportunity;

// The caller owns an already-subscribed listener for the entire session.
public static class AgentRunDispatchSession
{
    public static async Task RunAsync(
        Func<CancellationToken, Task<bool>> processNext,
        Func<CancellationToken, Task<DateTimeOffset?>> nextDue,
        Func<TimeSpan, CancellationToken, Task<bool>> wait,
        TimeSpan recoverySweep,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (await processNext(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            var due = await nextDue(cancellationToken);
            var delay = WaitInterval(due, timeProvider.GetUtcNow(), recoverySweep);
            // Signals buffered during drain remain on the dedicated listener connection.
            await wait(delay, cancellationToken);
        }
    }

    public static TimeSpan WaitInterval(
        DateTimeOffset? due, DateTimeOffset now, TimeSpan recoverySweep)
    {
        if (!due.HasValue) return recoverySweep;
        // Bound contention/clock-skew retries and cross the claim's strict lease inequality.
        var delay = due.Value - now;
        if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(1);
        return delay < recoverySweep ? delay : recoverySweep;
    }
}
