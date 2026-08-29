using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Persistence;

internal static class ApplicationDatabaseSession
{
    private const string SetApplicationRoleSql = "SET LOCAL ROLE advertified_app";

    public static async Task SetAsync(
        GovernanceDbContext dbContext,
        UserId? userId,
        TenantId? tenantId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Application database context requires an explicit transaction.");
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            SetApplicationRoleSql,
            cancellationToken);

        if (userId.HasValue)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('advertified.user_id', {userId.Value.Value.ToString()}, true)",
                cancellationToken);
        }

        if (tenantId.HasValue)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('advertified.tenant_id', {tenantId.Value.Value.ToString()}, true)",
                cancellationToken);
        }
    }
}
