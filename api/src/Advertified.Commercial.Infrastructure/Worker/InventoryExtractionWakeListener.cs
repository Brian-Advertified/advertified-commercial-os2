using Npgsql;

namespace Advertified.Commercial.Infrastructure.Worker;

public sealed class InventoryExtractionWakeListener : IAsyncDisposable
{
    internal const string ChannelName = "advertified_inventory_extraction";

    private readonly NpgsqlConnection connection;

    private InventoryExtractionWakeListener(NpgsqlConnection connection)
    {
        this.connection = connection;
    }

    internal static Task<InventoryExtractionWakeListener> OpenAsync(
        string connectionString,
        CancellationToken cancellationToken) =>
        OpenAsync(connectionString, ChannelName, cancellationToken);

    // Shared transport; the inventory entry point and channel remain unchanged.
    internal static async Task<InventoryExtractionWakeListener> OpenAsync(
        string connectionString,
        string channelName,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var role = new NpgsqlCommand(
                "SET ROLE advertified_worker", connection);
            await role.ExecuteNonQueryAsync(cancellationToken);
            using var identifiers = new NpgsqlCommandBuilder();
            await using var listen = new NpgsqlCommand(
                $"LISTEN {identifiers.QuoteIdentifier(channelName)}", connection);
            await listen.ExecuteNonQueryAsync(cancellationToken);
            return new InventoryExtractionWakeListener(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> WaitAsync(
        TimeSpan recoverySweepInterval,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(recoverySweepInterval);
        try
        {
            await connection.WaitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
