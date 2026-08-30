using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningSupplyValidation
{
    internal static bool IsFullyConfirmed(MediaPlanVersionView plan) =>
        plan.Lines.Count > 0 && plan.Lines.All(line =>
            line.SupplyConfidence == MasterDataCodes.SupplyConfidenceStatuses.Confirmed);
}
