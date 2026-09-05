namespace Advertified.Commercial.Application.Planning;

public sealed record InventoryPurchaseQuantity(
    Guid InventoryTenantId,
    Guid InventoryProductId,
    Guid ProductVersionId,
    Guid RateId,
    string RateType,
    int Quantity,
    int? Denominator = null);
