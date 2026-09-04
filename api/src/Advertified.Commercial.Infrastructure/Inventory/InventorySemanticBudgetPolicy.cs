namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySemanticBudgetPolicy
{
    // The overall operator-authorised ceiling is US$5.00. A conservative
    // US$0.188122 is reserved for confirmed and potentially billed historical
    // calls, leaving this maximum for all new corpus-certification requests.
    private const long MaximumNewSpendUsdMicros = 4_811_878;
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
