using System.Net;
using System.Text.Json;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class HealthEndpointTests
{
    private const string UnavailableConnection =
        "Host=127.0.0.1;Port=1;Database=closed;Username=closed;Password=closed;Timeout=1";

    [Fact]
    public async Task LivenessIsIndependentAndReadinessFailsClosed()
    {
        await using var factory = CreateFactory(UnavailableConnection);
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live");
        using var liveJson = await ReadJsonAsync(live);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("healthy", liveJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(["process"], Checks(liveJson));

        using var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();
        using var readyJson = JsonDocument.Parse(body);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("unavailable", readyJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(["process", "database-unavailable"], Checks(readyJson));
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ReadinessRequiresGovernedMasterData()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_readiness", "advertified_readiness", "readiness-local-only");
        await postgres.StartAsync();
        await DisposablePostgres.EnableRequiredExtensionsAsync(postgres.GetConnectionString());
        await DisposableDatabaseRoles.ProvisionAsync(postgres.GetConnectionString());
        await using var database = CreateDatabase(postgres.GetConnectionString());
        await database.Database.MigrateAsync();
        await using var factory = CreateFactory(postgres.GetConnectionString());
        using var client = factory.CreateClient();

        using var unseeded = await client.GetAsync("/health/ready");
        using var unseededJson = await ReadJsonAsync(unseeded);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unseeded.StatusCode);
        Assert.Equal(
            ["process", "database", "master-data-unavailable"],
            Checks(unseededJson));

        await new MasterDataBootstrapper(database, TimeProvider.System).ApplyAsync();
        using var ready = await client.GetAsync("/health/ready");
        using var readyJson = await ReadJsonAsync(ready);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("ready", readyJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(["process", "database", "master-data"], Checks(readyJson));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseDeterministicInventoryProtection();
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
        });

    private static GovernanceDbContext CreateDatabase(string connectionString) =>
        new(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var body = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(body);
    }

    private static string[] Checks(JsonDocument json) =>
        json.RootElement.GetProperty("checks").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
}
