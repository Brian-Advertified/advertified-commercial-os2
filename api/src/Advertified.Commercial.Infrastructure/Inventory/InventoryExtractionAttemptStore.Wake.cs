using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryExtractionAttemptStore
{
    private Task<int> SignalInventoryWorkAsync(
        Guid importId,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_notify(
                {InventoryExtractionWakeListener.ChannelName},
                {importId.ToString("N")})
            """, cancellationToken);
}
