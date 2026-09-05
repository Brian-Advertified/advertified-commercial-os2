using System.Globalization;

using Npgsql;

namespace Advertified.Commercial.Infrastructure.Worker;

public sealed partial class WorkerSchedulerStore(string connectionString)
{
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            return Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture) == 1;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    public async Task<Guid?> NextOutboxTenantAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.next_outbox_tenant()", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid value ? value : null;
    }

    public async Task<EmailWorkerClaim?> ClaimEmailAsync(
        Guid workerId,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT tenant_id, inbound_email_id, owner_user_id, correlation_id, claim_token
            FROM commercial.claim_next_email_work(@worker_id, @lease_seconds)
            """,
            connection);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EmailWorkerClaim(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetGuid(4))
            : null;
    }

    public async Task<bool> HeartbeatEmailAsync(
        Guid claimToken,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.heartbeat_email_work(@claim_token, @lease_seconds)",
            connection);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<string> CompleteEmailAsync(
        Guid claimToken,
        bool success,
        string? failureCode,
        int failureDelaySeconds,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT commercial.complete_email_work(
                @claim_token, @success, @failure_code,
                @failure_delay_seconds, @max_attempts)
            """,
            connection);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("success", success);
        command.Parameters.AddWithValue(
            "failure_code",
            failureCode is null ? DBNull.Value : failureCode);
        command.Parameters.AddWithValue("failure_delay_seconds", failureDelaySeconds);
        command.Parameters.AddWithValue("max_attempts", maxAttempts);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string ?? EmailWorkerCompletion.Fenced;
    }

    public async Task<InventoryExtractionWorkerClaim?> ClaimInventoryExtractionAsync(
        Guid workerId,
        int leaseSeconds,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT tenant_id, attempt_id, import_id, source_file_version, source_hash,
                status_code, stable_submission_key, provider_name, provider_version,
                external_task_id, submitted_at_utc, started_at_utc, last_polled_at_utc,
                polling_checkpoint, attempt_number, requested_by, command_id,
                correlation_id, claim_token
            FROM commercial.claim_next_inventory_extraction_attempt(
                @worker_id, @lease_seconds, @max_concurrency)
            """,
            connection);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        command.Parameters.AddWithValue("max_concurrency", maxConcurrency);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadInventoryExtractionClaim(reader)
            : null;
    }

    public async Task<bool> HeartbeatInventoryExtractionAsync(
        Guid claimToken,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.heartbeat_inventory_extraction_attempt(" +
            "@claim_token, @lease_seconds)", connection);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public Task<InventoryExtractionWakeListener> OpenInventoryExtractionListenerAsync(
        CancellationToken cancellationToken) =>
        InventoryExtractionWakeListener.OpenAsync(connectionString, cancellationToken);

    public async Task<DateTimeOffset?> NextInventoryExtractionDueAsync(
        int maxConcurrency, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.next_inventory_extraction_due(@max_concurrency)", connection);
        command.Parameters.AddWithValue("max_concurrency", maxConcurrency);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value switch
        {
            DateTime timestamp => new DateTimeOffset(timestamp),
            DateTimeOffset timestamp => timestamp,
            _ => null,
        };
    }

    private static InventoryExtractionWorkerClaim ReadInventoryExtractionClaim(
        NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt64(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
        reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
        reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
        reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
        reader.GetString(13), reader.GetInt32(14), reader.GetGuid(15), reader.GetGuid(16),
        reader.GetGuid(17), reader.GetGuid(18));

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var role = new NpgsqlCommand("SET ROLE advertified_worker", connection);
            await role.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
