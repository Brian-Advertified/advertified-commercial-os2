using System.Net;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertSupplierProofRequestBoundariesBeforeCompletionAsync(
        HttpClient buyer,
        HttpClient supplier,
        HttpClient other)
    {
        using var supplierRequests = await ReadAsync(
            supplier, SupplierTenantId, "delivery-proof-requests");
        Assert.Empty(supplierRequests.RootElement.EnumerateArray());

        using var buyerDenied = await buyer.GetAsync(
            $"/api/v1/tenants/{BuyerTenantId}/delivery-proof-requests");
        await AssertProblemAsync(
            buyerDenied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var crossTenant = await supplier.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/delivery-proof-requests");
        await AssertProblemAsync(
            crossTenant, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var otherRequests = await ReadAsync(
            other, OtherTenantId, "delivery-proof-requests");
        Assert.Empty(otherRequests.RootElement.EnumerateArray());
    }

    private static async Task AssertSupplierProofRequestDatabaseBoundaryAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await AssertDeliveryRequestFunctionDefinitionAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand(
            "SET LOCAL ROLE advertified_app", connection, transaction);
        await role.ExecuteNonQueryAsync();

        await SetDeliveryRequestContextAsync(
            connection, transaction, SupplierUserId, SupplierTenantId);
        Assert.Equal(1, await CountDeliveryRequestsAsync(connection, transaction));

        await SetDeliveryRequestContextAsync(
            connection, transaction, SupplierUserId, OtherTenantId);
        Assert.Equal(0, await CountDeliveryRequestsAsync(connection, transaction));

        await SetDeliveryRequestContextAsync(
            connection, transaction, BuyerUserId, BuyerTenantId);
        Assert.Equal(0, await CountDeliveryRequestsAsync(connection, transaction));
        await transaction.RollbackAsync();
    }

    private static async Task AssertDeliveryRequestFunctionDefinitionAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT routine.prosecdef,
                routine.proconfig @> ARRAY['search_path=pg_catalog']::text[],
                has_function_privilege(
                    'advertified_app', routine.oid, 'EXECUTE'),
                NOT EXISTS (
                    SELECT 1
                    FROM aclexplode(COALESCE(
                        routine.proacl,
                        acldefault('f', routine.proowner))) grant_item
                    WHERE grant_item.grantee = 0
                      AND grant_item.privilege_type = 'EXECUTE'),
                language.lanname,
                routine.prosrc
            FROM pg_proc routine
            JOIN pg_namespace scope ON scope.oid = routine.pronamespace
            JOIN pg_language language ON language.oid = routine.prolang
            WHERE scope.nspname = 'commercial'
              AND routine.proname = 'supplier_delivery_proof_requests'
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.Equal("sql", reader.GetString(4));
        var source = reader.GetString(5);
        Assert.Contains("LIMIT 200", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EXECUTE", source, StringComparison.OrdinalIgnoreCase);
        Assert.False(await reader.ReadAsync());
    }

    private static async Task SetDeliveryRequestContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        Guid tenantId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('advertified.user_id', $1, true),
                set_config('advertified.tenant_id', $2, true)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue(userId.ToString());
        command.Parameters.AddWithValue(tenantId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountDeliveryRequestsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.supplier_delivery_proof_requests()",
            connection,
            transaction);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Proof request count was unavailable."));
    }
}
