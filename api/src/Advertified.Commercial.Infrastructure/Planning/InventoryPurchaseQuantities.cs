using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryPurchaseQuantities
{
    private const int MaximumPurchasesPerAllocation = 1000;

    internal static void Validate(MediaAllocationView allocation)
    {
        var purchases = allocation.Purchases ?? [];
        if (purchases.Count > MaximumPurchasesPerAllocation || purchases.Any(item =>
                item.InventoryTenantId == Guid.Empty || item.InventoryProductId == Guid.Empty ||
                item.ProductVersionId == Guid.Empty || item.RateId == Guid.Empty || item.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(item.RateType)) ||
            purchases.Select(item => (item.InventoryTenantId, item.InventoryProductId)).Distinct().Count() != purchases.Count)
            throw new ArgumentException("Buying quantities require distinct exact products, rates and positive counts.");
    }

    internal static InventoryPurchaseQuantity? Find(PlanningInventoryRow inventory, MediaAllocationView allocation)
    {
        var purchase = allocation.Purchases?.SingleOrDefault(item =>
            item.InventoryTenantId == inventory.InventoryTenantId && item.InventoryProductId == inventory.ProductId);
        if (purchase is not null && (purchase.ProductVersionId != inventory.ProductVersionId ||
                purchase.RateId != inventory.RateId || purchase.RateType != inventory.RateType || purchase.Quantity <= 0))
            throw new UnpriceableRateException();
        return purchase;
    }
}
