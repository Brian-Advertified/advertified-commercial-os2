using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OpportunityRunDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentRuntimeOptions> options,
    TimeProvider timeProvider,
    ILogger<OpportunityRunDispatcher> logger) : BackgroundService
{
    private readonly Guid workerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMilliseconds(options.Value.PollMilliseconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogDispatchFailure(logger, exception);
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
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
        Message = "Opportunity run dispatch failed before a run was claimed.")]
    private static partial void LogDispatchFailure(ILogger logger, Exception exception);
}
