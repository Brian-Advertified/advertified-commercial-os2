using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ManualPartnerFundingMigrationTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task ExistingInactiveReferralBecomesAuditedManualRouteWithoutEnablingVodapay()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_manual_funding", "advertified_manual_funding", "advertified-manual-funding-local-only");
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await DisposablePostgres.EnableRequiredExtensionsAsync(connection);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>().UseNpgsql(connection).Options;
        await using var db = new GovernanceDbContext(options);
        var migrator = db.GetService<IMigrator>();
        // Resolve the repository's retained migration ID through EF's own name generator.
        var priorName = db.GetService<IMigrationsIdGenerator>().GetName("202609120011_OpportunityDuplicateRejection");
        await migrator.MigrateAsync(priorName);
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        // Initial previous-release reference state; subsequent steps are migration operations only.
        const string priorMetadata = """{"activationRequiresProviderIntegration":true,"mode":"REFERRAL"}""";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE governance.master_data_items SET is_active = false,
                metadata_json = {priorMetadata}::jsonb
            WHERE collection_code = 'paymentMethods' AND code = 'ADVERTISE_NOW_PAY_LATER'
            """);
        var historyBefore = await db.MasterDataItemHistory.CountAsync();
        await migrator.MigrateAsync();
        await AssertManualRouteAsync(db, expectedActive: true);
        Assert.True(await db.MasterDataItemHistory.CountAsync() > historyBefore);
        await migrator.MigrateAsync(priorName);
        await AssertManualRouteAsync(db, expectedActive: false);
        var historyAfterRollback = await db.MasterDataItemHistory.CountAsync();
        await migrator.MigrateAsync();
        await AssertManualRouteAsync(db, expectedActive: true);
        Assert.True(await db.MasterDataItemHistory.CountAsync() > historyAfterRollback);
    }

    private static async Task AssertManualRouteAsync(GovernanceDbContext db, bool expectedActive)
    {
        var active = await db.Database.SqlQueryRaw<bool>("""
            SELECT is_active AS "Value" FROM governance.master_data_items
            WHERE collection_code = 'paymentMethods' AND code = 'ADVERTISE_NOW_PAY_LATER'
            """).SingleAsync();
        Assert.Equal(expectedActive, active);
        var mode = await db.Database.SqlQueryRaw<string>("""
            SELECT metadata_json ->> 'mode' AS "Value" FROM governance.master_data_items
            WHERE collection_code = 'paymentMethods' AND code = 'ADVERTISE_NOW_PAY_LATER'
            """).SingleAsync();
        Assert.Equal(expectedActive ? "MANUAL_PARTNER_REFERRAL" : "REFERRAL", mode);
        Assert.False(await db.Database.SqlQueryRaw<bool>("""
            SELECT is_active AS "Value" FROM governance.master_data_items
            WHERE collection_code = 'paymentMethods' AND code = 'VODAPAY'
            """).SingleAsync());
    }
}
