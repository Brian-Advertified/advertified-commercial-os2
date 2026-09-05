namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySemanticBudgetPolicy
{
    internal static void Ensure(
        IReadOnlyList<InventorySemanticPacket> packets,
        InventorySemanticOptions settings)
    {
        if (packets.Any(packet =>
                packet.MaximumCostUsdMicros >
                settings.PerCallCostCapUsdMicros) ||
            packets.Sum(packet => packet.MaximumCostUsdMicros) >
                settings.CertificationBudgetUsdMicros)
        {
            throw new InventorySemanticBudgetExceededException();
        }
    }
}
