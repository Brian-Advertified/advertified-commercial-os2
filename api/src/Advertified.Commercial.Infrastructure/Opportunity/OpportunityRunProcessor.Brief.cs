using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Brief;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor
{
    private static readonly JsonSerializerOptions AgentArtifactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private async Task PersistBriefAsync(
        RunExecutionContext context,
        RunStrategyRow strategy,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        var generated = ParseBrief(context, execution.Output);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        var validated = await BriefCommandSupport.ValidateAsync(
            store.DbContext, generated.Command, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var briefId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var command = generated.Command with { BriefId = briefId };
        await BriefPersistence.InsertAggregateAndSourceAsync(
            store.DbContext,
            new BriefAggregateWrite(
                briefId, context.TenantId, context.Opportunity.ClientId,
                context.Opportunity.Id, context.Opportunity.Title + " campaign brief",
                context.Opportunity.OwnerUserId, MasterDataCodes.LifecycleStatuses.Created, 1, now),
            new BriefSourceWrite(
                sourceId, MasterDataCodes.BriefSourceTypes.Opportunity,
                "strategy:" + strategy.Id, "Approved opportunity strategy",
                strategy.ArtifactJson, OpportunityCommandSupport.Hash(strategy.ArtifactJson),
                context.ActorId.Value, now),
            cancellationToken);
        await BriefPersistence.InsertVersionAsync(
            store.DbContext,
            new BriefVersionWrite(
                versionId, context.TenantId, briefId, null, sourceId, 1,
                command, validated, execution.Output.EvidenceBindings.GetRawText(),
                MasterDataCodes.LifecycleStatuses.Draft, context.ActorId.Value, 1, now),
            cancellationToken);
        await BriefPersistence.BindEvidenceAsync(
            store.DbContext, context.TenantId, versionId,
            context.Evidence.Select(item => item.Id).ToArray(), cancellationToken);
        await BriefPersistence.SetCurrentDraftAsync(
            store.DbContext, context.TenantId, briefId, versionId, 1,
            MasterDataCodes.LifecycleStatuses.Draft, now, cancellationToken);
        await CompleteRunCoreAsync(context, execution.StepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static GeneratedBriefDraft ParseBrief(
        RunExecutionContext context,
        OpportunityAgentOutput output)
    {
        RequireObjectArtifact(output.Artifact);
        var artifact = JsonSerializer.Deserialize<GeneratedBriefArtifact>(
            output.Artifact.GetRawText(), AgentArtifactJson)
            ?? throw new InvalidOperationException("The agent Brief artifact is invalid.");
        var unknowns = JsonSerializer.Deserialize<BriefUnknownInput[]>(
            output.Unknowns.GetRawText(), BriefCommandSupport.StoredJson) ?? [];
        var assumptions = JsonSerializer.Deserialize<BriefAssumptionInput[]>(
            output.Assumptions.GetRawText(), BriefCommandSupport.StoredJson) ?? [];
        var command = new CreateBriefVersionCommand(
            Guid.Empty, null,
            Required(artifact.BusinessProblem),
            Required(artifact.Objective),
            Required(artifact.Audiences),
            Required(artifact.Geographies),
            Required(artifact.Timing),
            artifact.BudgetMinor,
            artifact.BudgetUnknown
                ?? throw new InvalidOperationException("The agent Brief artifact is incomplete."),
            artifact.Currency,
            artifact.VatStatus,
            artifact.FeesMinor,
            Required(artifact.Constraints),
            Required(artifact.Measurement),
            Required(artifact.Facts),
            unknowns,
            assumptions,
            Required(artifact.Conflicts),
            context.Evidence.Select(item => item.Id).ToArray());
        return new GeneratedBriefDraft(command);
    }

    private static string Required(string? value) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException("The agent Brief artifact is incomplete.");

    private static T[] Required<T>(T[]? values) =>
        values ?? throw new InvalidOperationException("The agent Brief artifact is incomplete.");
}

internal sealed record GeneratedBriefArtifact(
    string? BusinessProblem,
    string? Objective,
    string[]? Audiences,
    string[]? Geographies,
    string? Timing,
    long? BudgetMinor,
    bool? BudgetUnknown,
    string? Currency,
    string? VatStatus,
    long? FeesMinor,
    string[]? Constraints,
    string[]? Measurement,
    string[]? Facts,
    BriefConflictInput[]? Conflicts);

internal sealed record GeneratedBriefDraft(CreateBriefVersionCommand Command);
