using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Background;

public sealed class OutboxDispatchReadiness(
    IOptions<OutboxDispatchOptions> options,
    IOutboxTransport transport)
{
    public bool IsEnabled => options.Value.IsEnabled;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return true;
        }

        try
        {
            var health = await transport.CheckHealthAsync(cancellationToken);
            return health.IsAvailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
