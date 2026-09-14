using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static partial class PlanningHash
{
    internal static string ForBrief(PlanningBriefRow brief) =>
        OpportunityCommandSupport.Hash(
            $"{brief.Id:N}|{brief.Version}|{brief.Objective}|{brief.AudiencesJson}|" +
            $"{brief.GeographiesJson}|{brief.BudgetMinor}|{brief.Currency}|{brief.EvidenceIdsJson}");

    internal static string ForMix(
        PlanningBriefRow brief,
        Guid audienceId,
        string allocationsJson,
        string? impactEstimateJson = null) => OpportunityCommandSupport.Hash(
            $"{ForBrief(brief)}|{audienceId:N}|{allocationsJson}|{impactEstimateJson ?? "null"}");
}
