using Advertified.Commercial.Infrastructure.Outbox;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OutboxDispatchAcceptanceTests
{
    private static async Task ProveApplicationBoundaryAsync(
        string connectionString,
        Guid immutableEventId,
        Guid constrainedEventId,
        Guid otherTenantEventId)
    {
        var missingTenant = await Assert.ThrowsAsync<PostgresException>(() =>
            ClaimWithoutTenantContextAsync(connectionString));
        Assert.Equal("42501", missingTenant.SqlState);
        Assert.Equal(0, await CountOtherTenantEventsAsApplicationAsync(
            connectionString, TenantA));
        var updateFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsApplicationAsync(
                connectionString,
                TenantA,
                $"UPDATE commercial.outbox_messages SET attempts = 99 " +
                    $"WHERE id = '{otherTenantEventId}'"));
        Assert.Equal("42501", updateFailure.SqlState);
        var deleteFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsApplicationAsync(
                connectionString,
                TenantA,
                $"DELETE FROM commercial.outbox_messages " +
                    $"WHERE id = '{immutableEventId}'"));
        Assert.Equal("42501", deleteFailure.SqlState);

        var immutableFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(
                connectionString,
                $"UPDATE commercial.outbox_messages SET payload_json = '{{}}'::jsonb " +
                    $"WHERE id = '{immutableEventId}'"));
        Assert.Equal("P0001", immutableFailure.SqlState);
        var constraintFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(
                connectionString,
                $"UPDATE commercial.outbox_messages " +
                    $"SET published_at_utc = statement_timestamp(), " +
                    $"transport_reference = 'invalid-terminal-overlap' " +
                    $"WHERE id = '{constrainedEventId}'"));
        Assert.Equal("23514", constraintFailure.SqlState);
        var immutableDelete = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(
                connectionString,
                $"DELETE FROM commercial.outbox_messages " +
                    $"WHERE id = '{immutableEventId}'"));
        Assert.Equal("P0001", immutableDelete.SqlState);
        Assert.True(await HasLeastPrivilegeAsync(connectionString));
    }

    private static async Task ClaimWithoutTenantContextAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setRole = new NpgsqlCommand(
            "SET LOCAL ROLE advertified_app",
            connection,
            transaction))
        {
            await setRole.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT * FROM commercial.claim_next_outbox_event(gen_random_uuid(), 60)",
            connection,
            transaction);
        await command.ExecuteScalarAsync();
    }

    private static async Task ProveCrossTenantFunctionDenialAsync(
        string connectionString,
        OutboxDispatchClaim tenantBClaim)
    {
        var before = await FindEventAsync(
            connectionString,
            tenantBClaim.Envelope.EventId);
        await using var database = CreateDatabase(connectionString);
        var store = new OutboxDispatchStore(database);
        Assert.False(await store.HeartbeatAsync(
            TenantA,
            tenantBClaim.Envelope.EventId,
            tenantBClaim.ClaimToken,
            60,
            default));
        Assert.False(await store.AcknowledgeAsync(
            TenantA,
            tenantBClaim.Envelope.EventId,
            tenantBClaim.ClaimToken,
            "forged-cross-tenant-reference",
            default));
        Assert.False(await store.FailAsync(
            TenantA,
            tenantBClaim.Envelope.EventId,
            tenantBClaim.ClaimToken,
            false,
            "CROSS_TENANT_FORGERY",
            default));

        var after = await FindEventAsync(
            connectionString,
            tenantBClaim.Envelope.EventId);
        Assert.Equal(before.ClaimToken, after.ClaimToken);
        Assert.Equal(before.LeaseExpiresAtUtc, after.LeaseExpiresAtUtc);
        Assert.Equal(before.Attempts, after.Attempts);
        Assert.Null(after.PublishedAtUtc);
        Assert.Null(after.LastFailureCode);
        Assert.Null(after.DeadLetteredAtUtc);
    }

    private static async Task<long> CountOtherTenantEventsAsApplicationAsync(
        string connectionString,
        Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetApplicationContextAsync(connection, transaction, tenantId);
        await using var command = new NpgsqlCommand(
            $"SELECT count(*) FROM commercial.outbox_messages " +
                $"WHERE tenant_id <> '{tenantId}'",
            connection,
            transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsApplicationAsync(
        string connectionString,
        Guid tenantId,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetApplicationContextAsync(connection, transaction, tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetApplicationContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId)
    {
        await using (var setRole = new NpgsqlCommand(
            "SET LOCAL ROLE advertified_app",
            connection,
            transaction))
        {
            await setRole.ExecuteNonQueryAsync();
        }

        await using var setTenant = new NpgsqlCommand(
            "SELECT set_config('advertified.tenant_id', $1, true)",
            connection,
            transaction);
        setTenant.Parameters.AddWithValue(tenantId.ToString());
        await setTenant.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private const string LeastPrivilegeSql =
        """
        WITH dispatch_functions(function_oid) AS (
            VALUES
                ('commercial.claim_next_outbox_event(uuid,integer)'::regprocedure),
                ('commercial.heartbeat_outbox_event(uuid,uuid,integer)'::regprocedure),
                ('commercial.acknowledge_outbox_event(uuid,uuid,text)'::regprocedure),
                ('commercial.fail_outbox_event(uuid,uuid,boolean,text)'::regprocedure)
        ), helper_functions(function_oid) AS (
            VALUES
                ('commercial.lock_next_outbox_event(uuid,timestamptz)'::regprocedure),
                ('commercial.install_outbox_claim(uuid,uuid,uuid,timestamptz,timestamptz,boolean)'::regprocedure),
                ('commercial.dead_letter_exhausted_outbox_event(uuid,uuid,timestamptz,boolean)'::regprocedure)
        )
        SELECT bool_and(has_function_privilege(
                'advertified_app', function_oid, 'EXECUTE'))
            AND bool_and(function_metadata.prosecdef)
            AND bool_and(
                function_metadata.proconfig @> ARRAY['search_path=pg_catalog'])
            AND bool_and(function_metadata.proowner = (
                SELECT usesysid FROM pg_user WHERE usename = current_user))
            AND bool_and(function_metadata.proowner <> (
                SELECT oid FROM pg_roles WHERE rolname = 'advertified_app'))
            AND NOT EXISTS (
                SELECT 1
                FROM (
                    SELECT function_oid FROM dispatch_functions
                    UNION ALL
                    SELECT function_oid FROM helper_functions) public_function
                JOIN pg_proc public_metadata
                    ON public_metadata.oid = public_function.function_oid
                CROSS JOIN LATERAL aclexplode(COALESCE(
                    public_metadata.proacl,
                    acldefault('f', public_metadata.proowner))) privilege
                WHERE privilege.grantee = 0
                  AND privilege.privilege_type = 'EXECUTE')
            AND NOT EXISTS (
                SELECT 1
                FROM helper_functions
                JOIN pg_proc helper_metadata
                    ON helper_metadata.oid = helper_functions.function_oid
                WHERE has_function_privilege(
                        'advertified_app', function_oid, 'EXECUTE')
                   OR helper_metadata.prosecdef
                   OR NOT helper_metadata.proconfig
                        @> ARRAY['search_path=pg_catalog']
                   OR helper_metadata.proowner <> (
                        SELECT usesysid FROM pg_user
                        WHERE usename = current_user))
            AND to_regprocedure(
                'commercial.claim_next_outbox_event(uuid,timestamptz,timestamptz)')
                IS NULL
            AND to_regprocedure(
                'commercial.heartbeat_outbox_event(uuid,uuid,timestamptz,timestamptz)')
                IS NULL
            AND to_regprocedure(
                'commercial.acknowledge_outbox_event(uuid,uuid,timestamptz,text)')
                IS NULL
            AND to_regprocedure(
                'commercial.fail_outbox_event(uuid,uuid,timestamptz,boolean,text)')
                IS NULL
            AND NOT has_table_privilege(
                'advertified_app', 'commercial.outbox_messages', 'UPDATE')
            AND NOT has_table_privilege(
                'advertified_app', 'commercial.outbox_messages', 'DELETE')
        FROM dispatch_functions
        JOIN pg_proc function_metadata
            ON function_metadata.oid = dispatch_functions.function_oid
        """;

    private static async Task<bool> HasLeastPrivilegeAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(LeastPrivilegeSql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
