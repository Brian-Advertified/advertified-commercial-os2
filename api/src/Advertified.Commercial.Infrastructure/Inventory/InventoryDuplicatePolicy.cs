using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed record InventoryDuplicatePolicy(decimal SemanticCandidateThreshold)
{
    public static InventoryDuplicatePolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var item = registry.Collections.Single(collection =>
                collection.Code == MasterDataCodes.InventoryDuplicateMethods.Collection)
            .Items.Single(value =>
                value.Code == MasterDataCodes.InventoryDuplicateMethods.SemanticVector &&
                value.IsActive);
        using var metadata = JsonDocument.Parse(item.MetadataJson);
        var threshold = metadata.RootElement
            .GetProperty("duplicateCandidateThreshold").GetDecimal();
        return threshold is > 0 and <= 1
            ? new(threshold)
            : throw new InvalidOperationException(
                "The semantic duplicate candidate policy is invalid.");
    }
}
