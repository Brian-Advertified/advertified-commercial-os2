using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AiMonthlyBudgetTests
{
    private static readonly DateOnly Month = new(2026, 9, 1);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task LedgerIsGlobalHardCappedIdempotentAndMonthly()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_ai_monthly_budget",
            "advertified_ai_monthly_budget",
            "advertified-ai-monthly-budget-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var database = new GovernanceDbContext(options))
            await database.Database.MigrateAsync();

        var firstRun = Guid.NewGuid();
        var firstStep = Guid.NewGuid();
        Assert.True(await ReserveAsync(connectionString, Month,
            firstRun, firstStep, Guid.NewGuid(), 3_000_000));
        Assert.True(await ReserveAsync(connectionString, Month,
            firstRun, firstStep, await TenantAsync(connectionString,
                Month, firstRun, firstStep), 3_000_000));
        Assert.False(await ReserveAsync(connectionString, Month,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2_000_001));
        Assert.True(await ReserveAsync(connectionString, Month,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2_000_000));
        Assert.Equal(5_000_000, await ReadAsync(connectionString, Month));
        Assert.False(await ReserveAsync(connectionString, Month,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1));
        Assert.True(await ReserveAsync(connectionString, Month.AddMonths(1),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5_000_000));
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task CompletionUsesActualCostAndRejectsOverReservation()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_ai_budget_completion",
            "advertified_ai_budget_completion",
            "advertified-ai-budget-completion-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var database = new GovernanceDbContext(options))
            await database.Database.MigrateAsync();
        var run = Guid.NewGuid();
        var step = Guid.NewGuid();
        Assert.True(await ReserveAsync(connectionString, Month,
            run, step, Guid.NewGuid(), 1_000_000));
        Assert.False(await CompleteAsync(
            connectionString, Month, run, step, 1_000_001));
        Assert.True(await CompleteAsync(
            connectionString, Month, run, step, 125_000));
        Assert.Equal(125_000, await ReadAsync(connectionString, Month));
    }

    private static async Task<bool> ReserveAsync(
        string connectionString, DateOnly month, Guid run, Guid step,
        Guid tenant, long amount)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT governance.reserve_ai_monthly_budget(
                @month, @run, @step, @tenant, @amount)
            """, connection);
        command.Parameters.AddWithValue("month", month);
        command.Parameters.AddWithValue("run", run);
        command.Parameters.AddWithValue("step", step);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("amount", amount);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> CompleteAsync(
        string connectionString, DateOnly month, Guid run, Guid step,
        long amount)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT governance.complete_ai_monthly_budget(
                @month, @run, @step, @amount)
            """, connection);
        command.Parameters.AddWithValue("month", month);
        command.Parameters.AddWithValue("run", run);
        command.Parameters.AddWithValue("step", step);
        command.Parameters.AddWithValue("amount", amount);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private static async Task<long> ReadAsync(
        string connectionString, DateOnly month)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT governance.read_ai_monthly_budget(@month)", connection);
        command.Parameters.AddWithValue("month", month);
        return Assert.IsType<long>(await command.ExecuteScalarAsync());
    }

    private static async Task<Guid> TenantAsync(
        string connectionString, DateOnly month, Guid run, Guid step)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT tenant_id FROM governance.ai_monthly_budget_ledger
            WHERE month_start_utc = @month AND run_id = @run AND step_id = @step
            """, connection);
        command.Parameters.AddWithValue("month", month);
        command.Parameters.AddWithValue("run", run);
        command.Parameters.AddWithValue("step", step);
        return Assert.IsType<Guid>(await command.ExecuteScalarAsync());
    }
}
