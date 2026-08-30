using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

internal static class EmailAutomationPlanReadiness
{
    internal static void EnsureReady(MediaPlanVersionView plan)
    {
        if (plan.Lines.Count == 0 || plan.Lines.Any(line =>
                line.Channel is not (MasterDataCodes.Channels.Ooh or
                    MasterDataCodes.Channels.Dooh)))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.NonOohRequest,
                "The automatic proposal route accepts only out-of-home media.");
        }

        if (plan.Lines.Any(line =>
                line.SupplyConfidence != MasterDataCodes.SupplyConfidenceStatuses.Confirmed))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                "Current supplier-confirmed rates and availability are required before automatic delivery.");
        }

        if (plan.Objections.Any(item =>
                item.Resolution is null && item.Severity is
                    MasterDataCodes.CriticSeverities.Critical or
                    MasterDataCodes.CriticSeverities.Material))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.PlanUnready,
                "The media plan still has a material item that requires review.");
        }
    }
}
