using Advertified.Commercial.Infrastructure.Outbox;

namespace Advertified.Commercial.Api.Background;

public sealed partial class OutboxDispatchProcessor
{
    private async Task<bool> MaintainLeaseAsync(
        OutboxDispatchClaim claim,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    options.Value.HeartbeatInterval,
                    timeProvider,
                    cancellationToken);
                var renewed = await store.HeartbeatAsync(
                    claim.Envelope.TenantId,
                    claim.Envelope.EventId,
                    claim.ClaimToken,
                    options.Value.LeaseSeconds,
                    cancellationToken);
                if (!renewed)
                {
                    return false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return true;
            }
        }

        return true;
    }
}
