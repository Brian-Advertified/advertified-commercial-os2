using System.Text.Json.Nodes;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ProposalContractTests
{
    [Fact]
    public async Task ProposalApiPublishesVersionedApprovalDocumentAndClientDecisionBoundary()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseDeterministicInventoryProtection());
        using var client = factory.CreateClient();
        var contract = JsonNode.Parse(
            await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var paths = contract["paths"]!.AsObject();

        AssertPath(paths, "/briefs/", "/proposals:generate");
        AssertPath(paths, "/proposals/");
        AssertPath(paths, "/proposal-versions/", ":update");
        AssertPath(paths, "/proposal-versions/", ":approve");
        AssertPath(paths, "/proposal-versions/", ":render");
        AssertPath(paths, "/proposal-versions/", ":share");
        AssertPath(paths, "/proposal-versions/", ":select-option");
        AssertPath(paths, "/proposal-versions/", ":decline");
        AssertPath(paths, "/proposal-documents/");

        var schemas = contract["components"]!["schemas"]!.AsObject();
        var schema = schemas.First(item =>
            item.Value?["properties"]?["document"] is not null &&
            item.Value?["properties"]?["decision"] is not null &&
            item.Value?["properties"]?["expiryAtUtc"] is not null).Value!;
        var properties = schema["properties"]!.AsObject();
        Assert.NotNull(properties["options"]);
        Assert.NotNull(properties["document"]);
        Assert.NotNull(properties["decision"]);
        Assert.NotNull(properties["expiryAtUtc"]);
        Assert.NotNull(properties["version"]);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ProposalPersistenceAppliesWithTenantSecurityAndImmutableDecisions()
    {
        await using var postgres = new PostgreSqlBuilder(
                "advertified/postgres-dev:16-postgis3-pgvector0.8.6")
            .WithDatabase("advertified_proposal_contract")
            .WithUsername("advertified_proposal_contract")
            .WithPassword("advertified-proposal-contract-local-only")
            .Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var db = new GovernanceDbContext(options))
        {
            await db.Database.MigrateAsync();
        }
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var table in new[] {
            "proposal_versions", "proposal_options", "proposal_documents", "proposal_decisions" })
        {
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('commercial.' || @table) IS NOT NULL", connection);
            command.Parameters.AddWithValue("table", table);
            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
        await using var policy = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_policies
            WHERE schemaname = 'commercial'
              AND tablename IN ('proposal_versions','proposal_options','proposal_documents','proposal_decisions')
            """, connection);
        Assert.Equal(4L, (long)(await policy.ExecuteScalarAsync())!);
        await using var trigger = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_trigger trigger
            JOIN pg_class relation ON relation.oid = trigger.tgrelid
            JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'commercial'
              AND relation.relname IN ('proposal_options','proposal_documents','proposal_decisions')
              AND NOT trigger.tgisinternal
            """, connection);
        Assert.Equal(2L, (long)(await trigger.ExecuteScalarAsync())!);
    }

    private static void AssertPath(JsonObject paths, params string[] fragments)
    {
        Assert.Contains(paths, item => fragments.All(fragment =>
            item.Key.Contains(fragment, StringComparison.Ordinal)));
    }
}
