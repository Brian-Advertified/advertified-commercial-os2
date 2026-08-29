using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor(
    OpportunityRunStore runStore,
    OpportunityRecordStore store,
    IOpportunityAgentClient agentClient,
    TimeProvider timeProvider,
    ILogger<OpportunityRunProcessor> logger)
{
    private const string InterpretationArtifact = "BUSINESS_INTERPRETATION";
    private const string AngleArtifact = "SELECTED_OPPORTUNITY_ANGLE";
    private const string StrategyArtifact = "STRATEGY";

    public async Task ProcessClaimAsync(
        RunClaim claim,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = await LoadContextAsync(claim, cancellationToken);
            if (context.Run.InputVersion != context.Opportunity.Version)
            {
                throw new RunInputVersionDriftException();
            }

            switch (context.Run.RunKind)
            {
                case Gate4RunKinds.Interpretation:
                    await ProcessInterpretationAsync(context, cancellationToken);
                    break;
                case Gate4RunKinds.Angles:
                    await ProcessAnglesAsync(context, cancellationToken);
                    break;
                case Gate4RunKinds.StrategyCritic:
                    await ProcessStrategyCriticAsync(context, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("The claimed run kind is invalid.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogRunFailure(logger, claim.RunId, exception);
            await RecordFailureAsync(claim, exception, cancellationToken);
        }
    }

    private async Task ProcessInterpretationAsync(
        RunExecutionContext context,
        CancellationToken cancellationToken)
    {
        var execution = await ExecuteStepAsync(
            context,
            Gate4StepCodes.Interpretation,
            Gate4AgentCodes.BusinessInterpretation,
            [],
            cancellationToken);
        await PersistInterpretationAsync(context, execution, cancellationToken);
    }

    private async Task ProcessAnglesAsync(
        RunExecutionContext context,
        CancellationToken cancellationToken)
    {
        var interpretation = context.Interpretation
            ?? throw new InvalidOperationException("An approved interpretation is required.");
        var priors = new[]
        {
            Prior(InterpretationArtifact, interpretation.Id, interpretation.VersionNumber,
                interpretation.ArtifactJson),
        };
        var execution = await ExecuteStepAsync(
            context,
            Gate4StepCodes.Angles,
            Gate4AgentCodes.OpportunityIntelligence,
            priors,
            cancellationToken);
        await PersistAnglesAsync(context, interpretation, execution, cancellationToken);
    }

    private async Task ProcessStrategyCriticAsync(
        RunExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.CriticExists)
        {
            await CompleteRunAsync(context, Gate4StepCodes.Critic, cancellationToken);
            return;
        }

        var interpretation = context.Interpretation
            ?? throw new InvalidOperationException("An approved interpretation is required.");
        var angle = context.Angle
            ?? throw new InvalidOperationException("A selected angle is required.");
        var strategy = context.Strategy;
        if (strategy is null)
        {
            var strategyPriors = new[]
            {
                Prior(InterpretationArtifact, interpretation.Id, interpretation.VersionNumber,
                    interpretation.ArtifactJson),
                Prior(AngleArtifact, angle.Id, angle.Version, angle.ArtifactJson),
            };
            var strategyExecution = await ExecuteStepAsync(
                context,
                Gate4StepCodes.Strategy,
                Gate4AgentCodes.Strategy,
                strategyPriors,
                cancellationToken);
            strategy = await PersistStrategyAsync(
                context, interpretation, angle, strategyExecution, cancellationToken);
        }

        var criticPriors = new[]
        {
            Prior(StrategyArtifact, strategy.Id, strategy.VersionNumber, strategy.ArtifactJson),
        };
        var criticExecution = await ExecuteStepAsync(
            context,
            Gate4StepCodes.Critic,
            Gate4AgentCodes.CriticReadiness,
            criticPriors,
            cancellationToken);
        await PersistCriticAsync(context, strategy, criticExecution, cancellationToken);
    }

    private async Task<AgentStepExecution> ExecuteStepAsync(
        RunExecutionContext context,
        string stepCode,
        string agentCode,
        IReadOnlyList<AgentPriorArtifactInput> priors,
        CancellationToken cancellationToken)
    {
        var inputHash = CreateInputHash(context, agentCode, priors);
        var stepId = await PrepareStepAsync(
            context, stepCode, agentCode, inputHash, cancellationToken);
        var input = CreateInput(context, stepId, agentCode, priors);
        var output = await agentClient.InvokeAsync(input, cancellationToken);
        ValidateOutput(input, output);
        return new AgentStepExecution(stepId, stepCode, output);
    }

    private static string CreateInputHash(
        RunExecutionContext context,
        string agentCode,
        IReadOnlyList<AgentPriorArtifactInput> priors) =>
        OpportunityCommandSupport.Hash(JsonSerializer.Serialize(new
        {
            context.Run.InputVersion,
            AgentCode = agentCode,
            Evidence = context.Evidence.Select(item => new { item.Id, item.EvidenceSetVersion }),
            Priors = priors.Select(item => new { item.ArtifactId, item.Version }),
        }));

    private static AgentPriorArtifactInput Prior(
        string type,
        Guid id,
        long version,
        string json) => new(type, id, checked((int)version), ParseJson(json));

    private static void ValidateOutput(
        OpportunityAgentInput input,
        OpportunityAgentOutput output)
    {
        if (output.SchemaVersion != "1.0.0" ||
            output.Status is not (Gate4Statuses.Completed or Gate4Statuses.ReviewRequired) ||
            output.Usage.IncrementalCostMinor != 0 || output.Usage.ToolCalls != 0)
        {
            throw new InvalidOperationException("The agent output violated the Gate 4 contract.");
        }

        var approved = input.ApprovedEvidence.Select(item => item.Id).ToHashSet();
        var invalid = output.EvidenceBindings.ValueKind != JsonValueKind.Array ||
            output.EvidenceBindings.EnumerateArray()
                .SelectMany(BindingEvidenceIds)
                .Any(id => !approved.Contains(id));
        if (invalid)
        {
            throw new InvalidOperationException("The agent output referenced unapproved evidence.");
        }
    }

    private static IEnumerable<Guid> BindingEvidenceIds(JsonElement binding) =>
        binding.GetProperty("evidence_item_ids").EnumerateArray().Select(item => item.GetGuid());

    private static readonly Action<ILogger, Guid, Exception?> LogRunFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4002, nameof(OpportunityRunProcessor)),
            "Gate 4 run {RunId} stopped at a safe recovery boundary.");
}

internal sealed record AgentStepExecution(
    Guid StepId,
    string StepCode,
    OpportunityAgentOutput Output);

internal sealed class RunInputVersionDriftException : Exception;
