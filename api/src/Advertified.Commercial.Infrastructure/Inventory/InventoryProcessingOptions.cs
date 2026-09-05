using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryProcessingOptions
{
    public const string SectionName = "InventoryProcessing";

    // Maintenance is fail-closed across process restarts. Resume requires explicit configuration.
    public bool Paused { get; init; } = true;

    public void EnsureAdmission()
    {
        if (Paused) throw new InventoryProcessingPausedException();
    }
}
