using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertSupplierDecisionReportAsync(
        HttpClient buyer, HttpClient supplier, HttpClient other, string connectionString)
    {
        using var agency = await ReadAsync(buyer, BuyerTenantId,
            $"reporting/inventory-decisions?briefVersionId={BuyerBriefVersionId}");
        var privateItem = Assert.Single(agency.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(BuyerUserId, privateItem.GetProperty("actorId").GetGuid());
        Assert.Equal("Selected listing.", privateItem.GetProperty("reason").GetString());
        using var report = await ReadAsync(supplier, SupplierTenantId,
            $"reporting/inventory-decisions?inventoryProductId={ProductId}");
        Assert.True(report.RootElement.GetProperty("supplierSafe").GetBoolean());
        var item = Assert.Single(report.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(ProductVersionId, item.GetProperty("productVersionId").GetGuid());
        Assert.True(item.GetProperty("isSelected").GetBoolean());
        Assert.True(item.GetProperty("agentInterpreted").GetBoolean());
        foreach (var name in new[] { "actorId", "reason", "briefVersionId", "shortlistVersionId" })
            Assert.Equal(JsonValueKind.Null, item.GetProperty(name).ValueKind);
        foreach (var tenant in new[] { SupplierTenantId, OtherTenantId })
        {
            using var denied = await other.GetAsync(
                $"/api/v1/tenants/{tenant}/reporting/inventory-decisions?inventoryProductId={ProductId}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var campaignDenied = await supplier.GetAsync(
            $"/api/v1/tenants/{SupplierTenantId}/reporting/inventory-decisions?briefVersionId={BuyerBriefVersionId}");
        Assert.Equal(HttpStatusCode.Forbidden, campaignDenied.StatusCode);
        await AssertDecisionFunctionRejectsDirectCrossScopeAsync(connectionString);
    }

    private static async Task AssertDecisionFunctionRejectsDirectCrossScopeAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand("SET LOCAL ROLE advertified_app", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var context = new NpgsqlCommand("""
            SELECT set_config('advertified.tenant_id', $1, true), set_config('advertified.user_id', $2, true);
            """, connection, transaction);
        context.Parameters.AddWithValue(OtherTenantId.ToString());
        context.Parameters.AddWithValue(OtherUserId.ToString());
        await context.ExecuteNonQueryAsync();
        await using var read = new NpgsqlCommand(
            "SELECT * FROM commercial.read_inventory_decisions(NULL, $1, NULL, NULL, NULL)", connection, transaction);
        read.Parameters.AddWithValue(ProductId);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => read.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
    }
}
