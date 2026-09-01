using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OidcIdentityResolverTests
{
    private const string DatabaseName = "advertified_oidc_identity";
    private const string DatabaseUser = "advertified_oidc_identity";
    private const string DatabasePassword = "advertified-oidc-identity-local-only";
    private static readonly Guid UserId =
        Guid.Parse("db100000-0000-4000-8000-000000000001");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task VerifiedEmailBindsOneSubjectAndConflictsFailClosed()
    {
        await using var postgres = DisposablePostgres.Create(
            DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var database = new GovernanceDbContext(options))
        {
            await database.Database.MigrateAsync();
            await new MasterDataBootstrapper(database, new FixedTimeProvider()).ApplyAsync();
        }
        await SeedUserAsync(connectionString);

        await using var resolverDatabase = new GovernanceDbContext(options);
        var resolver = new OidcIdentityResolver(resolverDatabase, new FixedTimeProvider());
        var first = await resolver.ResolveAsync(
            "cognito", "subject-one", "owner@advertified.test", true,
            CancellationToken.None);
        Assert.Equal(UserId, first.UserId.Value);
        Assert.Equal(UserId, first.ActorId.Value);
        Assert.True(first.MfaRequired);

        var repeat = await resolver.ResolveAsync(
            "cognito", "subject-one", "changed@untrusted.test", false,
            CancellationToken.None);
        Assert.Equal(UserId, repeat.UserId.Value);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            resolver.ResolveAsync(
                "cognito", "subject-two", "owner@advertified.test", true,
                CancellationToken.None));

        await SetUserStatusAsync(connectionString, MasterDataCodes.LifecycleStatuses.Inactive);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            resolver.ResolveAsync(
                "cognito", "subject-one", "owner@advertified.test", true,
                CancellationToken.None));
    }

    private static async Task SeedUserAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO commercial.users (
                id, email, display_name, status_code, mfa_enabled,
                version, created_at_utc, updated_at_utc)
            VALUES (
                @id, 'owner@advertified.test', 'OIDC Owner', 'ACTIVE', TRUE,
                1, '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');
            """,
            connection);
        command.Parameters.AddWithValue("id", UserId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetUserStatusAsync(string connectionString, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE commercial.users SET status_code = @status WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("id", UserId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 1, 12, 30, 0, TimeSpan.Zero);
    }
}
