using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task EnsurePurchasesAsync(TenantId tenantId, MediaAllocationView[] allocations,
        CancellationToken cancellationToken)
    {
        foreach (var allocation in allocations) InventoryPurchaseQuantities.Validate(allocation);
        var purchases = allocations.SelectMany(item => item.Purchases ?? []).ToArray();
        if (purchases.Length == 0) return;
        var inventory = await store.ListInventoryAsync(tenantId, cancellationToken,
            purchases.Select(item => item.InventoryProductId).Distinct().ToArray());
        for (var index = 0; index < allocations.Length; index++)
        {
            var allocation = allocations[index];
            var normalized = new List<InventoryPurchaseQuantity>();
            foreach (var purchase in allocation.Purchases ?? [])
            {
                var product = inventory.SingleOrDefault(item => item.InventoryTenantId == purchase.InventoryTenantId &&
                    item.ProductId == purchase.InventoryProductId && item.ProductVersionId == purchase.ProductVersionId);
                if (product is null || product.Channel != allocation.Channel)
                    throw new UnauthorizedAccessException("Buying quantity product access denied.");
                _ = InventoryPurchaseQuantities.Find(product, allocation);
                _ = SupplierRateCalculator.Calculate(product, allocation.RunningPeriods, planningPolicy, purchase);
                normalized.Add(purchase with { Denominator = planningPolicy.RateQuantityDenominators[purchase.RateType] });
            }
            if (allocation.Purchases is not null) allocations[index] = allocation with { Purchases = normalized };
        }
    }
}
