using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PersistedCommandAcceptanceTests
{
    private static readonly TenantId Tenant =
        new(Guid.Parse("d1000000-0000-0000-0000-000000000001"));
    private static readonly ActorId Actor =
        new(Guid.Parse("d2000000-0000-0000-0000-000000000002"));
    private static readonly AgencyId Agency =
        new(Guid.Parse("d3000000-0000-0000-0000-000000000003"));
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task DuplicateCommandCommitsOneStateAndOutboxWithReplayAudit()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_command", "advertified_command", "advertified-command-local-only");
        await postgres.StartAsync();
        await DisposablePostgres.EnableRequiredExtensionsAsync(postgres.GetConnectionString());

        await DisposableDatabaseRoles.ProvisionAsync(postgres.GetConnectionString());
        await SeedTenantAsync(postgres.GetConnectionString());

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var dbContext = new GovernanceDbContext(options);
        var unitOfWork = new PersistedCommandUnitOfWork(
            dbContext,
            new FixedTimeProvider());
        var envelope = CreateEnvelope();
        var handlerCalls = 0;

        Task<CommandOutcome> Handler(CancellationToken _)
        {
            handlerCalls++;
            dbContext.Agencies.Add(new Agency(
                Agency,
                Tenant,
                "local-agency",
                "Local Agency Legal",
                "Local Agency",
                null,
                new LifecycleStatusCode("ACTIVE"),
                Now));
            return Task.FromResult(CreateOutcome(envelope));
        }

        var first = await unitOfWork.ExecuteOnceAsync(envelope, Handler, default);
        var replay = await unitOfWork.ExecuteOnceAsync(envelope, Handler, default);

        Assert.Equal(CommandDisposition.Applied, first.Disposition);
        Assert.Equal(CommandDisposition.Replayed, replay.Disposition);
        Assert.Equal(1, handlerCalls);
        Assert.Equal(first.Outcome.AggregateVersion, replay.Outcome.AggregateVersion);
        Assert.Equal(first.Outcome.Data.GetRawText(), replay.Outcome.Data.GetRawText());
        Assert.Equal(first.Outcome.Audit, replay.Outcome.Audit);
        Assert.Equal(first.Outcome.Outbox.EventId, replay.Outcome.Outbox.EventId);
        var additionalAudit = Assert.Single(first.Outcome.AdditionalAudits);
        var replayedAdditionalAudit = Assert.Single(replay.Outcome.AdditionalAudits);
        Assert.Equal(additionalAudit, replayedAdditionalAudit);
        var additionalOutbox = Assert.Single(first.Outcome.AdditionalOutbox);
        Assert.Equal(
            additionalOutbox.EventId,
            Assert.Single(replay.Outcome.AdditionalOutbox).EventId);
        Assert.Equal(
            first.Outcome.Outbox.Payload.GetRawText(),
            replay.Outcome.Outbox.Payload.GetRawText());

        await using var verification = new GovernanceDbContext(
            new DbContextOptionsBuilder<GovernanceDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options);
        Assert.Equal(1, await verification.Agencies.CountAsync());
        Assert.Equal(1, await verification.IdempotencyRecords.CountAsync());
        Assert.Equal(2, await verification.OutboxMessages.CountAsync());
        Assert.Equal(3, await verification.AuditEvents.CountAsync());
    }

    private static async Task SeedTenantAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new GovernanceDbContext(options);
        await dbContext.Database.MigrateAsync();
        await new MasterDataBootstrapper(dbContext, new FixedTimeProvider()).ApplyAsync();
        dbContext.Tenants.Add(new Tenant(
            Tenant,
            new TenantTypeCode("AGENCY"),
            "Command Tenant Legal",
            "Command Tenant",
            new Slug("command-tenant"),
            new LifecycleStatusCode("ACTIVE"),
            "Africa/Johannesburg",
            new CurrencyCode("ZAR"),
            new VatStatusCode("REGISTERED"),
            null,
            "{}",
            Now));
        await dbContext.SaveChangesAsync();
    }

    private static CommandEnvelope<CreateAgencyFixture> CreateEnvelope()
    {
        return new CommandEnvelope<CreateAgencyFixture>(
            Tenant,
            Actor,
            new CommandId(Guid.Parse("d4000000-0000-0000-0000-000000000004")),
            new CorrelationId(Guid.Parse("d5000000-0000-0000-0000-000000000005")),
            new IdempotencyKey("create-local-agency"),
            new Sha256Digest(new string('a', 64)),
            0,
            Now,
            new CreateAgencyFixture("Local Agency"));
    }

    private static CommandOutcome CreateOutcome(
        CommandEnvelope<CreateAgencyFixture> envelope)
    {
        var resource = new ResourceReference(
            new ResourceTypeCode("agency"),
            Agency.Value,
            1);
        return new CommandOutcome(
            JsonSerializer.SerializeToElement(new { id = Agency.Value, version = 1 }),
            1,
            new AuditRecord(
                Guid.Parse("d6000000-0000-0000-0000-000000000006"),
                envelope.TenantId,
                envelope.ActorId,
                envelope.CommandId,
                envelope.CorrelationId,
                new ActionCode("agency.created"),
                resource,
                Now),
            new OutboxMessage(
                Guid.Parse("d7000000-0000-0000-0000-000000000007"),
                envelope.TenantId,
                envelope.CommandId,
                envelope.CorrelationId,
                new EventTypeCode("AgencyCreated"),
                resource,
                JsonSerializer.SerializeToElement(new { id = Agency.Value }),
                Now),
            [new AuditRecord(
                Guid.Parse("d8000000-0000-0000-0000-000000000008"),
                envelope.TenantId,
                envelope.ActorId,
                envelope.CommandId,
                envelope.CorrelationId,
                new ActionCode("agency.created"),
                resource,
                Now)],
            [new OutboxMessage(
                Guid.Parse("d9000000-0000-0000-0000-000000000009"),
                envelope.TenantId,
                envelope.CommandId,
                envelope.CorrelationId,
                new EventTypeCode("AgencyCreated"),
                resource,
                JsonSerializer.SerializeToElement(new { id = Agency.Value }),
                Now)]);
    }

    private sealed record CreateAgencyFixture(string Name);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
