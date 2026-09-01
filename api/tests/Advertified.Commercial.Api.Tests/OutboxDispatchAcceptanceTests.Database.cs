using System.Text.Json;
using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Outbox;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OutboxDispatchAcceptanceTests
{
    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await using var database = CreateDatabase(connectionString);
        await database.Database.MigrateAsync();
        await new MasterDataBootstrapper(database, new MutableTimeProvider(StartedAt))
            .ApplyAsync();
        database.Tenants.AddRange(
            CreateTenant(TenantA, "outbox-tenant-a", "Outbox Tenant A"),
            CreateTenant(TenantB, "outbox-tenant-b", "Outbox Tenant B"));
        await database.SaveChangesAsync();
    }

    private static Tenant CreateTenant(Guid id, string slug, string name) => new(
        new TenantId(id),
        new TenantTypeCode(MasterDataCodes.TenantTypes.Agency),
        name,
        name,
        new Slug(slug),
        MasterDataReferences.LifecycleStatuses.Active,
        "Africa/Johannesburg",
        MasterDataReferences.Currencies.Zar,
        MasterDataReferences.VatStatuses.Registered,
        null,
        "{}",
        StartedAt);

    private static async Task SeedEventAsync(
        string connectionString,
        Guid eventId,
        Guid tenantId,
        DateTimeOffset occurredAtUtc)
    {
        var tenant = new TenantId(tenantId);
        var message = new OutboxMessage(
            eventId,
            tenant,
            new CommandId(Guid.NewGuid()),
            new CorrelationId(Guid.NewGuid()),
            MasterDataReferences.CommercialEventTypes.AgencyCreated,
            new ResourceReference(
                MasterDataReferences.CommercialResourceTypes.Tenant,
                tenantId,
                1),
            JsonSerializer.SerializeToElement(new { id = eventId }),
            occurredAtUtc);
        await using var database = CreateDatabase(connectionString);
        database.OutboxMessages.Add(new OutboxMessageRow(message));
        await database.SaveChangesAsync();
    }

    private static async Task<OutboxDispatchClaim?> ClaimAsync(
        string connectionString,
        Guid? tenantId = null,
        int leaseSeconds = 60)
    {
        await using var database = CreateDatabase(connectionString);
        var selection = await new OutboxDispatchStore(database).ClaimNextAsync(
            tenantId ?? TenantA,
            Guid.NewGuid(),
            leaseSeconds,
            default);
        return selection?.Claim;
    }

    private static async Task<bool> HeartbeatAsync(
        string connectionString,
        OutboxDispatchClaim claim,
        int leaseSeconds = 60)
    {
        await using var database = CreateDatabase(connectionString);
        return await new OutboxDispatchStore(database).HeartbeatAsync(
            claim.Envelope.TenantId,
            claim.Envelope.EventId,
            claim.ClaimToken,
            leaseSeconds,
            default);
    }

    private static async Task<bool> AcknowledgeAsync(
        string connectionString,
        OutboxDispatchClaim claim,
        string reference)
    {
        await using var database = CreateDatabase(connectionString);
        return await new OutboxDispatchStore(database).AcknowledgeAsync(
            claim.Envelope.TenantId,
            claim.Envelope.EventId,
            claim.ClaimToken,
            reference,
            default);
    }

    private static async Task<bool> FailAsync(
        string connectionString,
        OutboxDispatchClaim claim,
        bool terminal,
        string failureCode)
    {
        await using var database = CreateDatabase(connectionString);
        return await new OutboxDispatchStore(database).FailAsync(
            claim.Envelope.TenantId,
            claim.Envelope.EventId,
            claim.ClaimToken,
            terminal,
            failureCode,
            default);
    }

    private static async Task<bool> ProcessNextAsync(
        string connectionString,
        TimeProvider timeProvider,
        IOutboxTransport transport,
        OutboxDispatchOptions? dispatchOptions = null,
        OutboxDispatchMetrics? metrics = null,
        ILogger<OutboxDispatchProcessor>? logger = null)
    {
        await using var database = CreateDatabase(connectionString);
        var processor = new OutboxDispatchProcessor(
            new OutboxDispatchStore(database),
            transport,
            Options.Create(dispatchOptions ?? new OutboxDispatchOptions()),
            timeProvider,
            metrics ?? new OutboxDispatchMetrics(),
            logger ?? NullLogger<OutboxDispatchProcessor>.Instance);
        return await processor.ProcessNextAsync(TenantA, Guid.NewGuid(), default);
    }

    private static async Task ExpireClaimAsync(
        string connectionString,
        Guid eventId)
    {
        await using var database = CreateDatabase(connectionString);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.outbox_messages
            SET attempt_started_at_utc = statement_timestamp() - INTERVAL '2 minutes',
                lease_expires_at_utc = statement_timestamp() - INTERVAL '1 minute'
            WHERE id = {eventId}
            """);
    }

    private static async Task MakeRetryDueAsync(
        string connectionString,
        Guid eventId)
    {
        await using var database = CreateDatabase(connectionString);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.outbox_messages
            SET next_attempt_at_utc = statement_timestamp() - INTERVAL '1 second'
            WHERE id = {eventId}
            """);
    }

    private static async Task<DateTimeOffset> DatabaseNowAsync(string connectionString)
    {
        await using var database = CreateDatabase(connectionString);
        return await database.Database.SqlQuery<DateTimeOffset>(
            $"SELECT statement_timestamp() AS \"Value\"")
            .SingleAsync();
    }

    private static async Task WaitForBlockedHeartbeatAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event_type = 'Lock'
                  AND query LIKE '%heartbeat_outbox_event%')
            """,
            connection);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if ((bool)(await command.ExecuteScalarAsync())!)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The outbox heartbeat did not reach the locked row.");
    }

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
}
