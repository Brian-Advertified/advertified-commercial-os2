using Advertified.Commercial.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OutboxDispatchDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxDispatchOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatchDispatcher> logger) : BackgroundService
{
    private readonly Guid workerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(
                        options.Value.PollInterval,
                        timeProvider,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                LogDispatchFailure(logger);
                await Task.Delay(
                    options.Value.PollInterval,
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<OutboxDispatchProcessor>();
        return await processor.ProcessNextAsync(
            options.Value.TenantId!.Value,
            workerId,
            cancellationToken);
    }

    [LoggerMessage(
        EventId = 12_105,
        Level = LogLevel.Error,
        Message = "Outbox dispatch cycle failed; durable state remains available for recovery.")]
    private static partial void LogDispatchFailure(ILogger logger);
}
