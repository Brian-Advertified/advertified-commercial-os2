using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

[Trait("Category", "Migration")]
public sealed partial class SuppliedBriefPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = DisposablePostgres.Create(
        "advertified_brief_test_" + Guid.NewGuid().ToString("N"),
        "advertified_brief_test", "isolated-brief-test-only");

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await DisposablePostgres.EnableRequiredExtensionsAsync(connection);
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
    }

    public async Task DisposeAsync() => await postgres.DisposeAsync();

    private string JourneyConnection() => postgres.GetConnectionString();
}
