using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OutboxDispatchAcceptanceTests
{
    private const string DatabaseName = "advertified_outbox_dispatch";
    private const string DatabaseUser = "advertified_outbox_dispatch";
    private const string DatabasePassword = "advertified-outbox-dispatch-local-only";
    private static readonly Guid TenantA =
        Guid.Parse("cc100000-0000-4000-8000-000000000001");
    private static readonly Guid TenantB =
        Guid.Parse("cc100000-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset StartedAt =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Database")]
    public async Task ClaimsRetriesDeadLettersAndSecurityRemainDurable()
    {
        await using var postgres = DisposablePostgres.Create(
            DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await PrepareDatabaseAsync(connectionString);

        var racedEvent = Guid.Parse("cc200000-0000-4000-8000-000000000001");
        await ProveClaimFencingAsync(connectionString, racedEvent);

        var restartedEvent = Guid.Parse("cc200000-0000-4000-8000-000000000002");
        await ProveRestartIdempotencyAsync(connectionString, restartedEvent);

        var heartbeatRaceEvent = Guid.Parse("cc200000-0000-4000-8000-000000000008");
        await ProveHeartbeatCancellationRaceAsync(connectionString, heartbeatRaceEvent);

        var crashEvent = Guid.Parse("cc200000-0000-4000-8000-000000000007");
        await ProveCrashExhaustionAsync(connectionString, crashEvent);

        var retryEvent = Guid.Parse("cc200000-0000-4000-8000-000000000003");
        await ProveRetryScheduleAsync(connectionString, retryEvent);

        var terminalEvent = Guid.Parse("cc200000-0000-4000-8000-000000000004");
        await ProveTerminalFailureAsync(connectionString, terminalEvent);

        var timeoutEvent = Guid.Parse("cc200000-0000-4000-8000-000000000006");
        await ProvePublishTimeoutAsync(connectionString, timeoutEvent);

        var otherTenantEvent = Guid.Parse("cc200000-0000-4000-8000-000000000005");
        await SeedEventAsync(connectionString, otherTenantEvent, TenantB, StartedAt.AddHours(1));
        Assert.Null(await ClaimAsync(connectionString, TenantA));
        var otherTenantClaim = await ClaimAsync(connectionString, TenantB);
        Assert.NotNull(otherTenantClaim);
        Assert.Equal(otherTenantEvent, otherTenantClaim.Envelope.EventId);
        await ProveCrossTenantFunctionDenialAsync(
            connectionString,
            otherTenantClaim);
        Assert.True(await AcknowledgeAsync(
            connectionString,
            otherTenantClaim,
            "other-tenant-reference"));
        await ProveApplicationBoundaryAsync(
            connectionString,
            restartedEvent,
            retryEvent,
            otherTenantEvent);
    }

    private static async Task ProveClaimFencingAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt);
        var beforeClaim = await DatabaseNowAsync(connectionString);
        var claims = await Task.WhenAll(
            ClaimAsync(connectionString),
            ClaimAsync(connectionString));
        var afterClaim = await DatabaseNowAsync(connectionString);
        var firstClaim = Assert.Single(claims, claim => claim is not null)!;
        var firstState = await FindEventAsync(connectionString, eventId);
        Assert.InRange(firstState.AttemptStartedAtUtc!.Value, beforeClaim, afterClaim);
        Assert.Equal(
            TimeSpan.FromSeconds(60),
            firstState.LeaseExpiresAtUtc - firstState.AttemptStartedAtUtc);

        Assert.True(await HeartbeatAsync(connectionString, firstClaim));
        var heartbeated = await FindEventAsync(connectionString, eventId);
        Assert.True(heartbeated.LeaseExpiresAtUtc > firstState.LeaseExpiresAtUtc);
        Assert.Null(await ClaimAsync(connectionString));

        await ExpireClaimAsync(connectionString, eventId);
        Assert.False(await HeartbeatAsync(connectionString, firstClaim));
        Assert.False(await AcknowledgeAsync(
            connectionString, firstClaim, "expired-reference"));
        Assert.False(await FailAsync(
            connectionString, firstClaim, false, "EXPIRED_CLAIM"));

        var reclaimed = await ClaimAsync(connectionString);
        Assert.NotNull(reclaimed);
        Assert.NotEqual(firstClaim.ClaimToken, reclaimed.ClaimToken);
        Assert.Equal(2, reclaimed.Attempt);
        Assert.False(await AcknowledgeAsync(
            connectionString, firstClaim, "stale-reference"));
        Assert.False(await FailAsync(
            connectionString, firstClaim, false, "STALE_CLAIM"));
        Assert.True(await AcknowledgeAsync(
            connectionString, reclaimed, "accepted-reference"));
        Assert.False(await HeartbeatAsync(connectionString, reclaimed));
        Assert.Null(await ClaimAsync(connectionString));
    }

    private static async Task ProveRestartIdempotencyAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(1));
        var transport = new DeterministicOutboxTransport(
            Options.Create(new OutboxDispatchOptions
            {
                Mode = OutboxDispatchOptions.DeterministicMode,
            }));
        var first = await ClaimAsync(connectionString);
        Assert.NotNull(first);
        var firstResult = await transport.PublishAsync(first.Envelope, default);

        await ExpireClaimAsync(connectionString, eventId);
        var restarted = await ClaimAsync(connectionString);
        Assert.NotNull(restarted);
        var secondResult = await transport.PublishAsync(restarted.Envelope, default);
        Assert.Equal(first.Envelope.EventId, restarted.Envelope.EventId);
        Assert.Equal(firstResult.TransportReference, secondResult.TransportReference);
        Assert.Equal(2, transport.AttemptsFor(eventId));
        Assert.Equal(1, transport.AcceptedEventCount);
        Assert.True(await AcknowledgeAsync(
            connectionString,
            restarted,
            secondResult.TransportReference!));
    }

    private static async Task ProveCrashExhaustionAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(2));
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var claim = await ClaimAsync(connectionString);
            Assert.NotNull(claim);
            Assert.Equal(attempt, claim.Attempt);
            await ExpireClaimAsync(connectionString, eventId);
        }

        var logProvider = new CaptureLogProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(logProvider));
        using var counter = new OutboxCounterObserver(
            "advertified.outbox.dead_lettered");
        counter.Start();
        Assert.True(await ProcessNextAsync(
            connectionString,
            TimeProvider.System,
            new ScriptedOutboxTransport(Array.Empty<OutboxPublishResult>()),
            metrics: new OutboxDispatchMetrics(),
            logger: loggerFactory.CreateLogger<OutboxDispatchProcessor>()));
        Assert.Equal(1, counter.Total);
        var deadLetterLog = Assert.Single(logProvider.Records, record =>
            record.EventId.Id == 12_103 &&
            Equals(record.Properties["EventId"], eventId));
        Assert.Equal(
            "OUTBOX_LEASE_EXPIRED",
            deadLetterLog.Properties["FailureCode"]);
        var state = await FindEventAsync(connectionString, eventId);
        Assert.Equal(4, state.Attempts);
        Assert.Equal("OUTBOX_LEASE_EXPIRED", state.LastFailureCode);
        Assert.Equal(state.LastFailureAtUtc, state.DeadLetteredAtUtc);
        Assert.Null(state.ClaimToken);
        Assert.Null(state.NextAttemptAtUtc);
    }

    private static async Task ProveRetryScheduleAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(3));
        var transport = new ScriptedOutboxTransport(
            Enumerable.Repeat(
                OutboxPublishResult.TransientFailure("OUTBOX_TEST_TRANSIENT"),
                4));
        var expectedDelays = new[]
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10),
        };

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.True(await ProcessNextAsync(
                connectionString, TimeProvider.System, transport));
            var state = await FindEventAsync(connectionString, eventId);
            Assert.Equal(attempt, state.Attempts);
            Assert.Equal("OUTBOX_TEST_TRANSIENT", state.LastFailureCode);
            if (attempt < 4)
            {
                Assert.Equal(
                    expectedDelays[attempt - 1],
                    state.NextAttemptAtUtc - state.LastFailureAtUtc);
                await MakeRetryDueAsync(connectionString, eventId);
            }
            else
            {
                Assert.Equal(state.LastFailureAtUtc, state.DeadLetteredAtUtc);
                Assert.Null(state.NextAttemptAtUtc);
            }
        }

        Assert.False(await ProcessNextAsync(
            connectionString, TimeProvider.System, transport));
    }

    private static async Task ProveTerminalFailureAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(4));
        var transport = new ScriptedOutboxTransport(
            [OutboxPublishResult.TerminalFailure("OUTBOX_TEST_TERMINAL")]);
        Assert.True(await ProcessNextAsync(
            connectionString, TimeProvider.System, transport));
        var state = await FindEventAsync(connectionString, eventId);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(state.LastFailureAtUtc, state.DeadLetteredAtUtc);
        Assert.Equal("OUTBOX_TEST_TERMINAL", state.LastFailureCode);
        Assert.Null(state.NextAttemptAtUtc);
        Assert.Null(await ClaimAsync(connectionString));
    }

    private static async Task ProvePublishTimeoutAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(5));
        var options = new OutboxDispatchOptions { PublishTimeoutSeconds = 1 };
        Assert.True(await ProcessNextAsync(
            connectionString,
            TimeProvider.System,
            new BlockingOutboxTransport(),
            options));
        var state = await FindEventAsync(connectionString, eventId);
        Assert.Equal(1, state.Attempts);
        Assert.Equal("OUTBOX_TRANSPORT_TIMEOUT", state.LastFailureCode);
        Assert.Equal(
            TimeSpan.FromSeconds(30),
            state.NextAttemptAtUtc - state.LastFailureAtUtc);
        Assert.Null(state.DeadLetteredAtUtc);
    }
}
