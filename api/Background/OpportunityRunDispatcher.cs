using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;
using Advertified.Commercial.Infrastructure.Worker;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OpportunityRunDispatcher(
    IServiceScopeFactory scopeFactory,
    WorkerSchedulerStore scheduler,
    IOptions<AgentRuntimeOptions> options,
    TimeProvider timeProvider,
    ILogger<OpportunityRunDispatcher> logger) : BackgroundService
{
    private readonly Guid workerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var listener = await scheduler.OpenAgentRunListenerAsync(stoppingToken);
                    await AgentRunDispatchSession.RunAsync(
                        ProcessNextAsync, scheduler.NextAgentRunDueAsync,
                        async (delay, token) =>
                        {
                            var signalled = await listener.WaitAsync(delay, token);
                            if (failures > 0) LogRecovered(logger);
                            failures = 0;
                            return signalled;
                        },
                        TimeSpan.FromSeconds(options.Value.RecoverySweepSeconds),
                        timeProvider, stoppingToken);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    if (failures == 0) LogDispatchFailure(logger, exception);
                    failures = Math.Min(failures + 1, 7);
                    var seconds = Math.Min(options.Value.ReconnectMaxSeconds,
                        options.Value.ReconnectMinSeconds * Math.Pow(2, failures - 1));
                    await Task.Delay(TimeSpan.FromSeconds(seconds), timeProvider, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Includes cancellation during reconnect backoff and notification wait.
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runStore = scope.ServiceProvider.GetRequiredService<OpportunityRunStore>();
        var claim = await runStore.ClaimNextAsync(workerId, cancellationToken);
        if (claim is null)
        {
            return false;
        }

        var processor = scope.ServiceProvider.GetRequiredService<OpportunityRunProcessor>();
        await processor.ProcessClaimAsync(claim, cancellationToken);
        return true;
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Opportunity dispatch session failed; durable runs remain recoverable. Reconnecting with bounded backoff.")]
    private static partial void LogDispatchFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information,
        Message = "Opportunity dispatch recovered its subscription and durable queue check.")]
    private static partial void LogRecovered(ILogger logger);
}
