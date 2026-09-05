using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryScheduleEvidence
{
    internal static InventoryExtractedRow Create(
        int number, IReadOnlyDictionary<string, string> values,
        string offerLocator, string dateLocator, string timeLocator,
        string method, decimal? confidence = null,
        IReadOnlyDictionary<string, decimal?>? fieldConfidences = null)
    {
        var locators = values.Keys.ToDictionary(key => key, key => key switch
        {
            "scheduledate" => dateLocator,
            "timeslot" => timeLocator,
            _ => offerLocator,
        }, StringComparer.Ordinal);
        return new InventoryExtractedRow(number, offerLocator, values, method, confidence,
            locators, fieldConfidences,
            new Dictionary<string, string>
            {
                ["ratetype"] = MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
            },
            new Dictionary<string, string>
            {
                ["ratetype"] = MasterDataCodes.InventoryTransformationTypes.DerivedFromSourceContext,
            });
    }
}
