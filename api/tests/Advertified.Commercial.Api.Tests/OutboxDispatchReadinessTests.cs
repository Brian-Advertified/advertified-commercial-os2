using System.Net;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Outbox;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OutboxDispatchReadinessTests
{
    private static readonly Guid TenantId =
        Guid.Parse("cd100000-0000-4000-8000-000000000001");
    private static readonly Guid EventId =
        Guid.Parse("cd100000-0000-4000-8000-000000000002");
    private static readonly Guid UnavailableEventId =
        Guid.Parse("cd100000-0000-4000-8000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Database")]
    public async Task DisabledAndEnabledModesReportTruthfulReadiness()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_outbox_readiness",
            "advertified_outbox_readiness",
            "advertified-outbox-readiness-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await PrepareDatabaseAsync(connectionString);

        await using (var disabled = CreateFactory(connectionString, enabled: false))
        {
            using var client = disabled.CreateClient();
            using var ready = await client.GetAsync("/health/ready");
            using var json = await ReadJsonAsync(ready);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            Assert.Equal(["process", "database", "master-data"], Checks(json));
            Assert.Equal(0, (await FindEventAsync(connectionString)).Attempts);
        }

        await using (var enabled = CreateFactory(connectionString, enabled: true))
        {
            using var client = enabled.CreateClient();
            using var ready = await client.GetAsync("/health/ready");
            using var json = await ReadJsonAsync(ready);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            Assert.Equal(
                ["process", "database", "master-data", "outbox-transport"],
                Checks(json));
            var accepted = await WaitForAcceptanceAsync(connectionString);
            Assert.NotNull(accepted.PublishedAtUtc);
            Assert.Equal(1, accepted.Attempts);
            Assert.Equal($"deterministic:{EventId:D}", accepted.TransportReference);
        }

        await SeedPendingEventAsync(connectionString, UnavailableEventId);
        await using var unavailable = CreateFactory(
            connectionString,
            enabled: true,
            transportAvailable: false);
        using var unavailableClient = unavailable.CreateClient();
        using var response = await unavailableClient.GetAsync("/health/ready");
        using var unavailableJson = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            ["process", "database", "master-data", "outbox-transport-unavailable"],
            Checks(unavailableJson));
        var retained = await WaitForRetryAsync(
            connectionString,
            UnavailableEventId);
        Assert.Null(retained.PublishedAtUtc);
        Assert.Null(retained.DeadLetteredAtUtc);
        Assert.Equal("DETERMINISTIC_TRANSPORT_UNAVAILABLE", retained.LastFailureCode);
        Assert.NotNull(retained.NextAttemptAtUtc);
    }

    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await using var database = CreateDatabase(connectionString);
        await database.Database.MigrateAsync();
        await new MasterDataBootstrapper(database, new FixedTimeProvider()).ApplyAsync();
        database.Tenants.Add(new Tenant(
            new TenantId(TenantId),
            new TenantTypeCode(MasterDataCodes.TenantTypes.Agency),
            "Outbox Readiness Tenant",
            "Outbox Readiness Tenant",
            new Slug("outbox-readiness-tenant"),
            MasterDataReferences.LifecycleStatuses.Active,
            "Africa/Johannesburg",
            MasterDataReferences.Currencies.Zar,
            MasterDataReferences.VatStatuses.Registered,
            null,
            "{}",
            Now));
        database.OutboxMessages.Add(CreateMessage(EventId));
        await database.SaveChangesAsync();
    }

    private static OutboxMessageRow CreateMessage(Guid eventId)
    {
        var message = new OutboxMessage(
            eventId,
            new TenantId(TenantId),
            new CommandId(Guid.NewGuid()),
            new CorrelationId(Guid.NewGuid()),
            MasterDataReferences.CommercialEventTypes.AgencyCreated,
            new ResourceReference(
                MasterDataReferences.CommercialResourceTypes.Tenant,
                TenantId,
                1),
            JsonSerializer.SerializeToElement(new { id = eventId }),
            Now);
        return new OutboxMessageRow(message);
    }

    private static async Task SeedPendingEventAsync(
        string connectionString,
        Guid eventId)
    {
        await using var database = CreateDatabase(connectionString);
        database.OutboxMessages.Add(CreateMessage(eventId));
        await database.SaveChangesAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        bool enabled,
        bool transportAvailable = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting(
                $"{OutboxDispatchOptions.SectionName}:Mode",
                enabled
                    ? OutboxDispatchOptions.DeterministicMode
                    : OutboxDispatchOptions.DisabledMode);
            builder.UseSetting(
                $"{OutboxDispatchOptions.SectionName}:DeterministicTransportAvailable",
                transportAvailable.ToString());
            if (enabled)
            {
                builder.UseSetting(
                    $"{OutboxDispatchOptions.SectionName}:TenantId",
                    TenantId.ToString());
            }
            builder.UseDeterministicInventoryProtection();
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
        });

    private static async Task<OutboxMessageRow> WaitForAcceptanceAsync(
        string connectionString)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var row = await FindEventAsync(connectionString);
            if (row.PublishedAtUtc.HasValue)
            {
                return row;
            }

            await Task.Delay(50);
        }

        return await FindEventAsync(connectionString);
    }

    private static async Task<OutboxMessageRow> WaitForRetryAsync(
        string connectionString,
        Guid eventId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var row = await FindEventAsync(connectionString, eventId);
            if (row.LastFailureAtUtc.HasValue)
            {
                return row;
            }

            await Task.Delay(50);
        }

        return await FindEventAsync(connectionString, eventId);
    }

    private static async Task<OutboxMessageRow> FindEventAsync(string connectionString)
        => await FindEventAsync(connectionString, EventId);

    private static async Task<OutboxMessageRow> FindEventAsync(
        string connectionString,
        Guid eventId)
    {
        await using var database = CreateDatabase(connectionString);
        return await database.OutboxMessages.AsNoTracking()
            .SingleAsync(row => row.Id == eventId);
    }

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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
