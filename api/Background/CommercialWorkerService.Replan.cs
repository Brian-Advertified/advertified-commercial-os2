using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Worker;

namespace Advertified.Commercial.Api.Background;

public sealed partial class CommercialWorkerService
{
    private const string ProposalReplanFailure = "PROPOSAL_REPLAN_FAILED";

    private async Task<bool> ProcessProposalReplanAsync(
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var claim = await scheduler.ClaimProposalReplanAsync(
            workerId,
            settings.ProposalReplanLeaseSeconds,
            cancellationToken);
        if (claim is null)
            return false;

        using var processingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = MaintainProposalReplanLeaseAsync(
            claim,
            processingCancellation,
            heartbeatCancellation.Token);
        ProposalReplanAssessmentResult? assessment = null;
        string? failureDetail = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<ProposalReplanProcessor>();
            assessment = await processor.AssessAsync(
                claim, processingCancellation.Token);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
            when (processingCancellation.IsCancellationRequested)
        {
            failureDetail = "LEASE_LOST";
        }
        catch (Exception exception)
        {
            failureDetail = exception.GetType().Name;
            LogProposalReplanFailure(
                logger, claim.ReplanId, claim.SourceProposalVersionId, exception);
        }
        finally
        {
            heartbeatCancellation.Cancel();
            await heartbeat;
        }

        var completion = await scheduler.CompleteProposalReplanAsync(
            claim.ClaimToken,
            claim.SourceGeneration,
            assessment is not null,
            assessment?.ProposedRevisionJson,
            assessment?.ComparisonJson,
            assessment is null ? ProposalReplanFailure : null,
            failureDetail,
            settings.FailureDelaySeconds,
            settings.ProposalReplanMaxAttempts,
            cancellationToken);
        if (completion == EmailWorkerCompletion.Fenced)
            LogProposalReplanFenced(logger, claim.ReplanId);
        else if (completion == EmailWorkerCompletion.DeadLettered)
            LogProposalReplanDeadLettered(logger, claim.ReplanId);
        return true;
    }

    private async Task MaintainProposalReplanLeaseAsync(
        ProposalReplanWorkerClaim claim,
        CancellationTokenSource processingCancellation,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(10, options.Value.ProposalReplanLeaseSeconds / 3));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, timeProvider, cancellationToken);
                if (!await scheduler.HeartbeatProposalReplanAsync(
                        claim.ClaimToken,
                        options.Value.ProposalReplanLeaseSeconds,
                        cancellationToken))
                {
                    processingCancellation.Cancel();
                    LogProposalReplanLeaseLost(logger, claim.ReplanId);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    [LoggerMessage(
        EventId = 12_306,
        Level = LogLevel.Error,
        Message = "Proposal replan failed safely. ReplanId={ReplanId} ProposalVersionId={ProposalVersionId}")]
    private static partial void LogProposalReplanFailure(
        ILogger logger,
        Guid replanId,
        Guid proposalVersionId,
        Exception exception);

    [LoggerMessage(
        EventId = 12_307,
        Level = LogLevel.Warning,
        Message = "Proposal replan lease was lost. ReplanId={ReplanId}")]
    private static partial void LogProposalReplanLeaseLost(
        ILogger logger,
        Guid replanId);

    [LoggerMessage(
        EventId = 12_308,
        Level = LogLevel.Warning,
        Message = "Proposal replan completion was fenced. ReplanId={ReplanId}")]
    private static partial void LogProposalReplanFenced(
        ILogger logger,
        Guid replanId);

    [LoggerMessage(
        EventId = 12_309,
        Level = LogLevel.Error,
        Message = "Proposal replan reached the retry limit. ReplanId={ReplanId}")]
    private static partial void LogProposalReplanDeadLettered(
        ILogger logger,
        Guid replanId);
}
