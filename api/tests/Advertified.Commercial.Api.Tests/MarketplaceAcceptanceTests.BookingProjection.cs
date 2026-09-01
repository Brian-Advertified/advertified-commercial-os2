using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private const string ConfirmedSupplierNote =
        "Placement reserved only through this confirmed booking record.";

    private static async Task AssertBuyerSafeBookingProjectionAsync(
        HttpClient client,
        Guid bookingId)
    {
        using var list = await ReadAsync(client, BuyerTenantId, "bookings");
        var listed = Assert.Single(
            list.RootElement.EnumerateArray(),
            booking => booking.GetProperty("id").GetGuid() == bookingId);
        AssertClientSafeBooking(listed);

        using var detail = await ReadAsync(
            client, BuyerTenantId, $"bookings/{bookingId}");
        AssertClientSafeBooking(detail.RootElement);
    }

    private static void AssertClientSafeBooking(JsonElement booking)
    {
        Assert.False(booking.TryGetProperty("supplierCostMinor", out _));
        Assert.False(booking.TryGetProperty("supplierNote", out _));
        Assert.True(booking.GetProperty("clientPriceMinor").GetInt64() > 0);
        Assert.Equal("CONFIRMED", booking.GetProperty("status").GetString());
    }

    private static async Task AssertRoleAppropriateBookingProjectionsAsync(
        HttpClient agency,
        HttpClient supplier,
        HttpClient internalUser,
        Guid bookingId)
    {
        await AssertBuyerSafeBookingProjectionAsync(agency, bookingId);
        await AssertBuyerSafeBookingProjectionAsync(internalUser, bookingId);
        await AssertSupplierFieldsVisibleAsync(
            supplier, SupplierTenantId, bookingId);
    }

    private static async Task AssertSupplierFieldsVisibleAsync(
        HttpClient client,
        Guid tenantId,
        Guid bookingId)
    {
        using var list = await ReadAsync(client, tenantId, "bookings");
        var listed = Assert.Single(
            list.RootElement.EnumerateArray(),
            booking => booking.GetProperty("id").GetGuid() == bookingId);
        AssertSupplierFieldsVisible(listed);

        using var detail = await ReadAsync(
            client, tenantId, $"bookings/{bookingId}");
        AssertSupplierFieldsVisible(detail.RootElement);
    }

    private static void AssertSupplierFieldsVisible(JsonElement booking)
    {
        Assert.True(booking.GetProperty("supplierCostMinor").GetInt64() > 0);
        Assert.Equal(ConfirmedSupplierNote,
            booking.GetProperty("supplierNote").GetString());
    }

    private static async Task ChangeClientBookingRoleAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE commercial.memberships
            SET role_code = $1, version = version + 1,
                updated_at_utc = CURRENT_TIMESTAMP
            WHERE tenant_id = $2 AND user_id = $3
            """,
            connection);
        command.Parameters.AddWithValue(MasterDataCodes.Roles.AdvertiserAdmin);
        command.Parameters.AddWithValue(BuyerTenantId);
        command.Parameters.AddWithValue(ClientUserId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
