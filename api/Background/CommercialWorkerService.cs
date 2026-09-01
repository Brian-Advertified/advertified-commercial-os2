using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Outbox;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed partial class CommercialWorkerService(
    IServiceScopeFactory scopeFactory,
    WorkerSchedulerStore scheduler,
    IOptions<WorkerDispatchOptions> options,
    IOptions<EmailAutomationOptions> emailAutomation,
    IOptions<OutboxDispatchOptions> outboxDispatch,
    TimeProvider timeProvider,
    ILogger<CommercialWorkerService> logger) : BackgroundService
{
    private const string EmailWorkerFailure = "EMAIL_WORKER_FAILURE";
    private readonly Guid workerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var outboxProcessed = ShouldProcessOutbox(outboxDispatch.Value) &&
                    await ProcessOutboxAsync(stoppingToken);
                var emailProcessed = emailAutomation.Value.Mode != EmailAutomationOptions.DisabledMode &&
                    await ProcessEmailAsync(stoppingToken);
                if (!outboxProcessed && !emailProcessed)
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
            catch (Exception exception)
            {
                LogWorkerCycleFailure(logger, exception);
                await Task.Delay(options.Value.PollInterval, timeProvider, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        var tenantId = await scheduler.NextOutboxTenantAsync(cancellationToken);
        if (tenantId is null)
        {
            return false;
        }
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxDispatchProcessor>();
        return await processor.ProcessNextAsync(
            tenantId.Value,
            workerId,
            cancellationToken);
    }

    private async Task<bool> ProcessEmailAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var claim = await scheduler.ClaimEmailAsync(
            workerId,
            settings.EmailLeaseSeconds,
            cancellationToken);
        if (claim is null)
        {
            return false;
        }

        using var processingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = MaintainEmailLeaseAsync(
            claim,
            processingCancellation,
            heartbeatCancellation.Token);
        var succeeded = false;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<IEmailProposalAutomationProcessor>();
            await processor.ProcessAsync(
                new TenantId(claim.TenantId),
                new ActorId(claim.OwnerUserId),
                claim.InboundEmailId,
                new CorrelationId(claim.CorrelationId),
                processingCancellation.Token);
            succeeded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
        {
            // Lease loss is already logged by the heartbeat loop. The stale worker is fenced.
        }
        catch (Exception exception)
        {
            LogEmailFailure(logger, claim.InboundEmailId, claim.CorrelationId, exception);
        }
        finally
        {
            heartbeatCancellation.Cancel();
            await heartbeat;
        }

        var completion = await scheduler.CompleteEmailAsync(
            claim.ClaimToken,
            succeeded,
            succeeded ? null : EmailWorkerFailure,
            settings.FailureDelaySeconds,
            settings.MaxEmailAttempts,
            cancellationToken);
        if (completion == EmailWorkerCompletion.Fenced)
        {
            LogEmailCompletionFenced(logger, claim.InboundEmailId, claim.CorrelationId);
        }
        else if (completion == EmailWorkerCompletion.DeadLettered)
        {
            LogEmailDeadLettered(logger, claim.InboundEmailId, claim.CorrelationId);
        }
        return true;
    }

    private async Task MaintainEmailLeaseAsync(
        EmailWorkerClaim claim,
        CancellationTokenSource processingCancellation,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(10, options.Value.EmailLeaseSeconds / 3));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, timeProvider, cancellationToken);
                if (!await scheduler.HeartbeatEmailAsync(
                        claim.ClaimToken,
                        options.Value.EmailLeaseSeconds,
                        cancellationToken))
                {
                    processingCancellation.Cancel();
                    LogEmailLeaseLost(logger, claim.InboundEmailId, claim.CorrelationId);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private static bool ShouldProcessOutbox(OutboxDispatchOptions settings) =>
        settings.IsEnabled &&
        !(settings.Mode == OutboxDispatchOptions.DeterministicMode &&
          settings.TenantId is not null);

    [LoggerMessage(
        EventId = 12_301,
        Level = LogLevel.Error,
        Message = "Commercial worker cycle failed; durable work remains queued.")]
    private static partial void LogWorkerCycleFailure(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 12_302,
        Level = LogLevel.Error,
        Message = "Inbound email work failed safely. EmailId={EmailId} CorrelationId={CorrelationId}")]
    private static partial void LogEmailFailure(
        ILogger logger,
        Guid emailId,
        Guid correlationId,
        Exception exception);

    [LoggerMessage(
        EventId = 12_303,
        Level = LogLevel.Warning,
        Message = "Inbound email worker lease was lost. EmailId={EmailId} CorrelationId={CorrelationId}")]
    private static partial void LogEmailLeaseLost(
        ILogger logger,
        Guid emailId,
        Guid correlationId);

    [LoggerMessage(
        EventId = 12_304,
        Level = LogLevel.Warning,
        Message = "Inbound email worker completion was fenced. EmailId={EmailId} CorrelationId={CorrelationId}")]
    private static partial void LogEmailCompletionFenced(
        ILogger logger,
        Guid emailId,
        Guid correlationId);

    [LoggerMessage(
        EventId = 12_305,
        Level = LogLevel.Error,
        Message = "Inbound email work reached the retry limit and was dead-lettered. EmailId={EmailId} CorrelationId={CorrelationId}")]
    private static partial void LogEmailDeadLettered(
        ILogger logger,
        Guid emailId,
        Guid correlationId);
}
