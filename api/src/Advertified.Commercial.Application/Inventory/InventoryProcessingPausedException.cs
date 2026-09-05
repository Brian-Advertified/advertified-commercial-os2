namespace Advertified.Commercial.Application.Inventory;

public sealed class InventoryProcessingPausedException : Exception
{
    public InventoryProcessingPausedException() : base("Inventory processing is paused.") { }
}
