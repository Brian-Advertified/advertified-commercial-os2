namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryExtractionSourceAccounting
{
    private sealed record SourceElement(
        string Locator,
        string StructureId,
        string StructureKind,
        int Row,
        int Column,
        string RawValue,
        string? PositionJson,
        string? Signal);
}
