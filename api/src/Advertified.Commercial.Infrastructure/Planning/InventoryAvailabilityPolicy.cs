using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryAvailabilityPolicy
{
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static bool IsAvailable(
        PlanningInventoryRow inventory,
        IReadOnlyList<MediaRunningPeriodView> requested)
    {
        if (inventory.Availability == MasterDataCodes.AvailabilityStatuses.Unavailable)
        {
            return false;
        }
        try
        {
            var periods = JsonSerializer.Deserialize<InventoryUnavailablePeriod[]>(
                inventory.UnavailablePeriodsJson, StoredJson) ?? [];
            return !periods.Any(blocked => requested.Any(period =>
                blocked.Start <= period.End && blocked.End >= period.Start));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
