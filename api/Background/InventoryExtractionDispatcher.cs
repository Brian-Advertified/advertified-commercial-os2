using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed partial class InventoryExtractionDispatcher(
    IServiceScopeFactory scopeFactory,
    WorkerSchedulerStore scheduler,
    IOptions<WorkerDispatchOptions> options,
    TimeProvider timeProvider,
    ILogger<InventoryExtractionDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerIds = Enumerable
            .Range(0, options.Value.InventoryExtractionMaxConcurrency)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunListenerSessionAsync(workerIds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogCycleFailure(logger, exception);
                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.FailureDelaySeconds),
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    private async Task RunListenerSessionAsync(
        IReadOnlyList<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        await using var listener = await scheduler
            .OpenInventoryExtractionListenerAsync(cancellationToken);
        await DrainAvailableAsync(workerIds, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var due = await scheduler.NextInventoryExtractionDueAsync(
                options.Value.InventoryExtractionMaxConcurrency, cancellationToken);
            var wait = options.Value.InventoryExtractionRecoverySweepInterval;
            if (due.HasValue)
            {
                var activeDelay = due.Value - timeProvider.GetUtcNow();
                if (activeDelay < TimeSpan.FromSeconds(1)) activeDelay = TimeSpan.FromSeconds(1);
                if (activeDelay < wait) wait = activeDelay;
            }
            await listener.WaitAsync(
                wait,
                cancellationToken);
            await DrainAvailableAsync(workerIds, cancellationToken);
        }
    }

    private async Task DrainAvailableAsync(
        IReadOnlyList<Guid> workerIds,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var processed = await Task.WhenAll(workerIds.Select(
                workerId => ProcessNextAsync(workerId, cancellationToken)));
            if (!processed.Any(value => value))
            {
                return;
            }
        }
    }

    private async Task<bool> ProcessNextAsync(
        Guid workerId,
        CancellationToken cancellationToken)
    {
        var leaseSeconds = options.Value.InventoryExtractionLeaseSeconds;
        var claim = await scheduler.ClaimInventoryExtractionAsync(
            workerId, leaseSeconds, options.Value.InventoryExtractionMaxConcurrency,
            cancellationToken);
        if (claim is null)
        {
            return false;
        }
        using var processingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = MaintainLeaseAsync(
            claim, processingCancellation, heartbeatCancellation.Token);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<InventoryExtractionAttemptProcessor>();
            await processor.ProcessAsync(claim, processingCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
        {
            LogLeaseLost(logger, claim.AttemptId, claim.CorrelationId);
        }
        finally
        {
            heartbeatCancellation.Cancel();
            await heartbeat;
        }
        return true;
    }

    private async Task MaintainLeaseAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationTokenSource processingCancellation,
        CancellationToken cancellationToken)
    {
        var leaseSeconds = options.Value.InventoryExtractionLeaseSeconds;
        var interval = TimeSpan.FromSeconds(Math.Max(10, leaseSeconds / 3));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, timeProvider, cancellationToken);
                if (!await scheduler.HeartbeatInventoryExtractionAsync(
                        claim.ClaimToken, leaseSeconds, cancellationToken))
                {
                    processingCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    [LoggerMessage(
        EventId = 12_401,
        Level = LogLevel.Error,
        Message = "Inventory extraction dispatch failed; durable attempt state remains recoverable.")]
    private static partial void LogCycleFailure(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 12_402,
        Level = LogLevel.Warning,
        Message = "Inventory extraction lease was lost. AttemptId={AttemptId} CorrelationId={CorrelationId}")]
    private static partial void LogLeaseLost(
        ILogger logger,
        Guid attemptId,
        Guid correlationId);
}
