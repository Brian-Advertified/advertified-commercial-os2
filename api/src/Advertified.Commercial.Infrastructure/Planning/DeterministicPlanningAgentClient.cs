using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class DeterministicPlanningAgentClient : IPlanningAgentClient
{
    public Task<PlanningAgentProposal> ProposeAsync(
        PlanningBriefInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.BudgetMinor < 0 || input.Currency.Length == 0)
        {
            throw new ArgumentException("The approved planning budget is invalid.");
        }
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
                input.EvidenceItemIds.Count > 0 ? 0.70m : 0.45m)).ToArray();
        var channels = input.AvailableChannels.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).Take(3).ToArray();
        if (channels.Length == 0)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = Allocate(input.BudgetMinor, channels);
        return Task.FromResult(new PlanningAgentProposal(
            audiences,
            allocations,
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
