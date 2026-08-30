using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class DeterministicPlanningAgentClient : IPlanningAgentClient
{
    public Task<AudienceAgentProposal> ProposeAudiencesAsync(
        PlanningBriefInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var classification = input.EvidenceItemIds.Count > 0
            ? MasterDataCodes.EvidenceClassifications.Inference
            : MasterDataCodes.EvidenceClassifications.Hypothesis;
        var audiences = input.Audiences.Select((name, index) =>
            new AudienceDefinitionProposal(
                name,
                $"People described by the approved Brief as {name}.",
                input.Objective,
                "Buying context is not supplied and remains a planning question.",
                input.Geographies,
                null,
                null,
                null,
                classification,
                ["Do not infer sensitive individual attributes."],
                input.EvidenceItemIds,
                input.EvidenceItemIds.Count > 0 ? 0.70m : 0.45m,
                true)).ToArray();
        var audienceNames = audiences.Select(item => item.Name).ToArray();
        var targetingRationale = audienceNames.Length == 0
            ? "No target segment was supplied; audience clarification is required."
            : $"Prioritise {string.Join(", ", audienceNames)} in {string.Join(", ", input.Geographies)} because the approved Brief identifies them as the audiences and markets relevant to the objective.";
        var positioningStatement = audienceNames.Length == 0
            ? $"Position the campaign around the approved objective: {input.Objective}"
            : $"For {string.Join(", ", audienceNames)}, position the advertised offer as the credible route to {input.Objective.ToLowerInvariant()}.";
        return Task.FromResult(new AudienceAgentProposal(
            audiences,
            targetingRationale,
            positioningStatement,
            ["Audience size, reach and response baseline are not supplied."],
            "The deterministic proposal uses only the approved Brief.",
            "deterministic",
            "fixture-v1",
            0));
    }

    public Task<MediaPlanningAgentProposal> ProposeMediaMixAsync(
        MediaPlanningInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.BudgetMinor < 0 || input.Currency.Length == 0)
        {
            throw new ArgumentException("The approved planning budget is invalid.");
        }
        var channels = input.AvailableChannels.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).Take(3).ToArray();
        if (channels.Length == 0)
        {
            throw new InvalidLifecycleTransitionException();
        }
        return Task.FromResult(new MediaPlanningAgentProposal(
            Allocate(input.BudgetMinor, channels),
            ["Audience size, reach and response baseline are not supplied."],
            ["Channel allocations are an internal planning hypothesis pending plan review."],
            "The deterministic proposal uses only the approved Brief and published channel set.",
            "deterministic",
            "fixture-v1",
            0));
    }

    private static MediaAllocationProposal[] Allocate(long budget, string[] channels)
    {
        var even = budget / channels.Length;
        var remainder = budget % channels.Length;
        return channels.Select((channel, index) => new MediaAllocationProposal(
            channel,
            even + (index == 0 ? remainder : 0),
            index == 0 ? "Primary response channel" : "Supporting reach channel",
            Array.Empty<MediaRunningPeriodInput>()))
            .ToArray();
    }
}
