using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Proposal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static void ConfigureEmailAgentFixtures(IServiceCollection services)
    {
        services.RemoveAll<ISuppliedBriefAgentClient>();
        services.AddScoped<ISuppliedBriefAgentClient>(_ => new SuppliedBriefAgentFixture(EmailUnderstanding));
        services.RemoveAll<IPlanningAgentClient>();
        services.AddScoped<IPlanningAgentClient, PlanningAgentFixture>();
        services.RemoveAll<IProposalNarrativeClient>();
        services.AddScoped<IProposalNarrativeClient>(_ => new ProposalNarrativeFixture(ProposalPolicy.Load()));
    }

    private static SuppliedBriefUnderstandingView EmailUnderstanding(SuppliedBriefAgentInput input)
    {
        var multiChannel = input.SourceContent == MultiChannelBriefBody;
        var missingAudience = input.SourceContent == IncompleteBriefBody &&
            !input.Clarifications.Any(item => item.FieldPath == AudienceFieldPath);
        var result = SuppliedBriefAgentFixture.Create(input, multiChannel
            ? MasterDataCodes.CampaignModes.FullCampaign : MasterDataCodes.CampaignModes.OohOnly);
        return result with
        {
            ClientName = "Email OOH Client",
            RequiresHumanClarification = missingAudience,
            Questions = missingAudience ? [new(AudienceFieldPath, "Who should this campaign reach?", true, [])] : [],
            Draft = result.Draft with
            {
                Objective = "Increase qualified enquiries",
                Audiences = missingAudience ? [] : ["Local business decision makers"],
                Geographies = ["Johannesburg"], Timing = "2026-09-01 to 2026-09-30",
                BudgetMinor = 1_000_000, Measurement = ["Qualified enquiries"],
                Unknowns = missingAudience ? [new(AudienceFieldPath, "Who should this campaign reach?", true)] : [],
            },
        };
    }
}
