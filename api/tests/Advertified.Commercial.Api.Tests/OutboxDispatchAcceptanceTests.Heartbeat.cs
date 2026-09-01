using Advertified.Commercial.Infrastructure.Outbox;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OutboxDispatchAcceptanceTests
{
    private static async Task ProveHeartbeatCancellationRaceAsync(
        string connectionString,
        Guid eventId)
    {
        await SeedEventAsync(connectionString, eventId, TenantA, StartedAt.AddSeconds(2));
        var transport = new ControlledOutboxTransport();
        var options = new OutboxDispatchOptions
        {
            LeaseSeconds = 10,
            HeartbeatSeconds = 1,
            PublishTimeoutSeconds = 15,
        };
        var processing = ProcessNextAsync(
            connectionString,
            TimeProvider.System,
            transport,
            options);
        await transport.WaitUntilStartedAsync();

        await using var blocker = new NpgsqlConnection(connectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT id FROM commercial.outbox_messages WHERE id = $1 FOR UPDATE",
            blocker,
            transaction))
        {
            command.Parameters.AddWithValue(eventId);
            await command.ExecuteScalarAsync();
        }

        await WaitForBlockedHeartbeatAsync(connectionString);
        transport.Accept(eventId);
        await Task.Delay(100);
        await transaction.CommitAsync();
        Assert.True(await processing);
        var state = await FindEventAsync(connectionString, eventId);
        Assert.NotNull(state.PublishedAtUtc);
        Assert.Equal($"controlled:{eventId:D}", state.TransportReference);
        Assert.Equal(1, state.Attempts);
    }
}
