namespace Advertified.Commercial.Application.Inventory;

public sealed record InventoryPlaceView(
    Guid Id, string Name, string? Category, string Context,
    decimal Latitude, decimal Longitude, Guid ProductVersionId,
    Guid SourceImportId, string SourceLocator);
