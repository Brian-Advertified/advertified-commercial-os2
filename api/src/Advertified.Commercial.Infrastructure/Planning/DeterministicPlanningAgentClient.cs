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

    public Task<InventoryIntelligenceAgentProposal> InterpretInventoryAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Candidates.Count == 0 ||
            input.Candidates.Select(item => item.CandidateId).Distinct().Count() !=
                input.Candidates.Count)
        {
            throw new ArgumentException("Inventory Intelligence requires unique candidates.");
        }
        foreach (var candidate in input.Candidates)
        {
            ValidateInventoryCandidate(candidate);
        }
        var interpretations = input.Candidates
            .Select(candidate => new InventoryCandidateInterpretationProposal(
                candidate.CandidateId,
                ExplainInventory(candidate)))
            .ToArray();
        return Task.FromResult(new InventoryIntelligenceAgentProposal(
            interpretations,
            "Each explanation restates governed eligibility, audience-fit and benchmark facts.",
            "deterministic",
            "fixture-v1",
            0));
    }

    private static void ValidateInventoryCandidate(
        InventoryIntelligenceCandidateInput candidate)
    {
        if (candidate.CandidateId == Guid.Empty || candidate.ProductVersionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(candidate.Name))
        {
            throw new ArgumentException("The inventory candidate identity is invalid.");
        }
        var valid = candidate.IsEligible
            ? candidate.RateAmountMinor.HasValue &&
              !string.IsNullOrWhiteSpace(candidate.Currency) &&
              candidate.Score is >= 0 and <= 1 &&
              candidate.RejectionReason is null &&
              candidate.RejectionDetail is null
            : !string.IsNullOrWhiteSpace(candidate.RejectionReason) &&
              !string.IsNullOrWhiteSpace(candidate.RejectionDetail) &&
              !candidate.Score.HasValue;
        if (!valid)
        {
            throw new ArgumentException("The inventory eligibility facts are invalid.");
        }
        if (candidate.AudienceFit.EvidenceGaps is null ||
            !ValidAudienceScore(candidate.AudienceFit.LanguageScore) ||
            !ValidAudienceScore(candidate.AudienceFit.LifeStageScore) ||
            !ValidAudienceScore(candidate.AudienceFit.LsmSemScore))
        {
            throw new ArgumentException("The inventory audience-fit facts are invalid.");
        }
    }

    private static string ExplainInventory(InventoryIntelligenceCandidateInput candidate)
    {
        if (!candidate.IsEligible)
        {
            return $"Excluded by governed hard eligibility: {candidate.RejectionDetail}";
        }
        var audience = ExplainAudienceFit(candidate.AudienceFit);
        var benchmark = candidate.Benchmark;
        if (benchmark is null)
        {
            return $"{candidate.Name} is eligible after governed hard constraints. " +
                "No deterministic comparative benchmark applies, so selection should rely " +
                $"on the visible rate, supply state and campaign fit. {audience}";
        }
        var basis = benchmark.GeographyBasis.Replace('_', ' ').ToLowerInvariant();
        if (benchmark.CohortSize < 2 || !benchmark.MedianMinor.HasValue)
        {
            return $"{candidate.Name} is eligible after governed hard constraints. " +
                $"The {basis} benchmark has {benchmark.CohortSize} compatible peer(s), " +
                $"which is insufficient for a defensible market-price conclusion. {audience}";
        }
        var position = benchmark.Position.Replace('_', ' ').ToLowerInvariant();
        var confidence = decimal.ToInt32(decimal.Truncate(benchmark.Confidence * 100));
        return $"{candidate.Name} is eligible after governed hard constraints. Its " +
            $"published rate is {position} across {benchmark.CohortSize} compatible peers " +
            $"using {basis}; deterministic benchmark confidence is {confidence}%. {audience}";
    }

    private static string ExplainAudienceFit(InventoryAudienceFitView fit)
    {
        if (fit.EvidenceGaps.Count > 0)
        {
            return "Audience fit remains unscored because evidence is incomplete: " +
                string.Join(", ", fit.EvidenceGaps) + ".";
        }
        var scores = new[]
        {
            ScoreText("language", fit.LanguageScore),
            ScoreText("life-stage", fit.LifeStageScore),
            ScoreText("LSM/SEM", fit.LsmSemScore),
        }.Where(value => value is not null).ToArray();
        return scores.Length == 0
            ? "The approved target audiences contain no structured audience dimensions to compare."
            : "Evidence-backed audience fit: " + string.Join(", ", scores!) + ".";
    }

    private static string? ScoreText(string dimension, decimal? value) => value.HasValue
        ? $"{dimension} {decimal.ToInt32(decimal.Truncate(value.Value * 100))}%"
        : null;

    private static bool ValidAudienceScore(decimal? value) => value is null or >= 0 and <= 1;

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
