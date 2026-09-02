using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Booking;

internal static class BookingJson
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static InventorySupplierCommercialValues? ReadSupplier(string? json) =>
        Read<InventorySupplierCommercialValues>(json);

    internal static InventoryCommercialTermsValues? ReadTerms(string? json) =>
        Read<InventoryCommercialTermsValues>(json);

    internal static InventoryDeliverableValues? ReadDeliverable(string? json) =>
        Read<InventoryDeliverableValues>(json);

    internal static InventorySpatialValues? ReadSpatial(string? json) =>
        Read<InventorySpatialValues>(json);

    private static T? Read<T>(string? json) where T : class => json is null
        ? null
        : JsonSerializer.Deserialize<T>(json, StoredJson)
          ?? throw new InvalidOperationException("Stored booking JSON is invalid.");
}
