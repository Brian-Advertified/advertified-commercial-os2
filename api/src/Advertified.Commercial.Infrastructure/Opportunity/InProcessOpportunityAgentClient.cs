using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class InProcessOpportunityAgentClient : IOpportunityAgentClient
{
    private static readonly string[] StrategyObjectives = ["Increase qualified enquiries"];
    private static readonly string[] StrategyAudiences = ["People with a demonstrated need"];
    private static readonly string[] StrategyChannels = ["Prioritise measurable response channels"];
    private static readonly string[] StrategyRisks = ["Audience and conversion baselines remain unknown"];

    public Task<OpportunityAgentOutput> InvokeAsync(
        OpportunityAgentInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.ApprovedEvidence.Count == 0)
        {
            throw new EvidenceRequiredException();
        }

        var output = input.AgentCode switch
        {
            MasterDataCodes.AgentTypes.BusinessInterpretation => Interpretation(input),
            MasterDataCodes.AgentTypes.OpportunityIntelligence => Angles(input),
            MasterDataCodes.AgentTypes.Strategy => Strategy(input),
            MasterDataCodes.AgentTypes.CriticReadiness => Critic(),
            MasterDataCodes.AgentTypes.BriefDrafting => Brief(input),
            _ => throw new ArgumentException("The agent code is not enabled.", nameof(input)),
        };
        return Task.FromResult(output);
    }

    private static OpportunityAgentOutput Interpretation(OpportunityAgentInput input)
    {
        var first = input.ApprovedEvidence[0];
        var artifact = JsonSerializer.SerializeToElement(new
        {
            offering = first.Excerpt,
            customer_groups = Array.Empty<string>(),
            buying_occasions = Array.Empty<string>(),
            geographies = Array.Empty<string>(),
            commercial_context = "Interpretation is limited to approved source claims.",
        });
        return Output(
            MasterDataCodes.LifecycleStatuses.Completed,
            artifact,
            Bindings("artifact.offering", input),
            JsonSerializer.SerializeToElement(new[]
            {
                new { field_path = "artifact.customer_groups", question = "Who buys?", is_blocking = false },
            }),
            [],
            "Only approved evidence was interpreted.");
    }

    private static OpportunityAgentOutput Angles(OpportunityAgentInput input)
    {
        var ids = input.ApprovedEvidence.Select(item => item.Id).ToArray();
        var artifact = JsonSerializer.SerializeToElement(new
        {
            angles = new[]
            {
                new
                {
                    rank = 1,
                    title = "Make the verified offering easier to discover",
                    rationale = "Use the approved offering context as the factual anchor.",
                    evidence_item_ids = ids,
                    confidence = 0.80m,
                },
                new
                {
                    rank = 2,
                    title = "Turn verified interest into qualified enquiries",
                    rationale = "Test response while treating conversion behaviour as unknown.",
                    evidence_item_ids = ids,
                    confidence = 0.65m,
                },
            },
        });
        return Output(
            MasterDataCodes.LifecycleStatuses.Completed, artifact, Bindings("artifact.angles", input),
            JsonSerializer.SerializeToElement(Array.Empty<object>()), [],
            "The alternatives are proposals linked to approved evidence.");
    }

    private static OpportunityAgentOutput Strategy(OpportunityAgentInput input)
    {
        var artifact = JsonSerializer.SerializeToElement(new
        {
            diagnosis = "The verified offering needs a clearer path from discovery to enquiry.",
            growth_thesis = "Use the selected evidence-backed angle to create measurable demand.",
            objectives = StrategyObjectives,
            audience_hypotheses = StrategyAudiences,
            proposition = "A practical response to the documented need.",
            message = "Move from interest to a clear next step.",
            channel_implications = StrategyChannels,
            risks = StrategyRisks,
        });
        return Output(
            MasterDataCodes.LifecycleStatuses.Completed, artifact, Bindings("artifact.diagnosis", input),
            JsonSerializer.SerializeToElement(Array.Empty<object>()), [],
            "The strategy remains an evidence-bound proposal.");
    }

    private static OpportunityAgentOutput Critic()
    {
        var artifact = JsonSerializer.SerializeToElement(new
        {
            readiness = MasterDataCodes.LifecycleStatuses.ReviewRequired,
            summary = "Resolve the measurement gap before approval.",
        });
        var objections = new[]
        {
            new AgentObjectionOutput(
                MasterDataCodes.CriticSeverities.Material,
                "artifact.objectives",
                "No approved conversion baseline is present.",
                "Record the baseline as unknown and define a measurement task."),
        };
        return Output(
            MasterDataCodes.LifecycleStatuses.ReviewRequired, artifact,
            JsonSerializer.SerializeToElement(Array.Empty<object>()),
            JsonSerializer.SerializeToElement(Array.Empty<object>()), objections,
            "The critic retained the evidence gap.");
    }

    private static OpportunityAgentOutput Brief(OpportunityAgentInput input)
    {
        var strategy = input.PriorArtifacts.Single(item => item.ArtifactType == MasterDataCodes.WorkflowStepTypes.Strategy).Value;
        var objectives = strategy.GetProperty("objectives").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        var audiences = strategy.GetProperty("audience_hypotheses").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        var constraints = strategy.GetProperty("risks").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        var artifact = JsonSerializer.SerializeToElement(new
        {
            business_problem = strategy.GetProperty("diagnosis").GetString(),
            objective = objectives.FirstOrDefault() ?? "Objective requires confirmation",
            audiences,
            geographies = Array.Empty<string>(),
            timing = "Timing not supplied",
            budget_minor = (long?)null,
            budget_unknown = true,
            currency = (string?)null,
            vat_status = (string?)null,
            fees_minor = (long?)null,
            constraints,
            measurement = Array.Empty<string>(),
            facts = input.ApprovedEvidence.Select(item => item.Excerpt).ToArray(),
            conflicts = Array.Empty<object>(),
        });
        var unknowns = JsonSerializer.SerializeToElement(new[]
        {
            new { field_path = "budget", question = "What budget is available?", is_blocking = false },
            new { field_path = "timing", question = "When must the work run?", is_blocking = false },
        });
        return Output(
            MasterDataCodes.LifecycleStatuses.Completed, artifact, Bindings("artifact.business_problem", input),
            unknowns, [], "The Brief draft preserves unsupported commercial details as unknown.");
    }

    private static JsonElement Bindings(string fieldPath, OpportunityAgentInput input)
    {
        return JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                field_path = fieldPath,
                evidence_item_ids = input.ApprovedEvidence.Select(item => item.Id).ToArray(),
            },
        });
    }

    private static OpportunityAgentOutput Output(
        string status,
        JsonElement artifact,
        JsonElement bindings,
        JsonElement unknowns,
        IReadOnlyList<AgentObjectionOutput> objections,
        string rationale) => new(
        "1.0.0",
        status,
        artifact,
        bindings,
        unknowns,
        JsonSerializer.SerializeToElement(Array.Empty<object>()),
        objections,
        rationale,
        new AgentUsageOutput("deterministic", "fixture-v1", 0, 0, 0, "FIXTURE"));
}
