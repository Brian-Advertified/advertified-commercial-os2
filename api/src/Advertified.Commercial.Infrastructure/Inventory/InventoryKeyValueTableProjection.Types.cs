namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryKeyValueTableProjection
{
    private sealed class ProjectionState
    {
        internal SortedDictionary<string, string> Values { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Locators { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Bases { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Transformations { get; } =
            new(StringComparer.Ordinal);
        internal List<string> Geographies { get; } = [];
        internal List<RateOption> Rates { get; } = [];
        internal string? GeographyLocator { get; set; }
    }

    private sealed record Pair(
        int Row,
        string Label,
        string Value,
        int ValueColumn);

    private sealed record RateOption(
        int Priority,
        string Value,
        string Locator);
}
