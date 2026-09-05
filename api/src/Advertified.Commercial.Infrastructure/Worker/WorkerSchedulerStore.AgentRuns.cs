using Npgsql;

namespace Advertified.Commercial.Infrastructure.Worker;

public sealed partial class WorkerSchedulerStore
{
    private const string AgentRunChannel = "advertified_agent_run";

    public Task<InventoryExtractionWakeListener> OpenAgentRunListenerAsync(
        CancellationToken cancellationToken) =>
        InventoryExtractionWakeListener.OpenAsync(
            connectionString, AgentRunChannel, cancellationToken);

    public async Task<DateTimeOffset?> NextAgentRunDueAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.next_agent_run_due()", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value switch
        {
            DateTime timestamp => new DateTimeOffset(timestamp),
            DateTimeOffset timestamp => timestamp,
            _ => null,
        };
    }
}
