using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Brief;

public sealed record UnderstandSuppliedBriefRequest(
    string SourceTitle,
    string SourceContent,
    IReadOnlyList<BriefClarificationInput>? Clarifications = null);

public sealed record BriefClarificationInput(
    string FieldPath,
    string Value);

public sealed record SuppliedBriefQuestionView(
    string FieldPath,
    string Question,
    bool IsBlocking,
    IReadOnlyList<string> Options);

public sealed record SuppliedBriefEvidenceView(
    string FieldPath,
    string Kind,
    string Excerpt,
    decimal Confidence,
    string SourceLocator);

public sealed record SuppliedBriefAgentUsageView(
    string Provider,
    string Model,
    string PromptVersion,
    string ResearchStatus,
    int ToolCalls,
    long IncrementalCostMinor);

public sealed record SuppliedBriefDraftView(
    string BusinessProblem,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    string Timing,
    long? BudgetMinor,
    bool BudgetUnknown,
    string? Currency,
    string? VatStatus,
    long? FeesMinor,
    IReadOnlyList<string> MediaRequirements,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Measurement,
    IReadOnlyList<string> Facts,
    IReadOnlyList<BriefUnknownInput> Unknowns,
    IReadOnlyList<BriefAssumptionInput> Assumptions,
    IReadOnlyList<BriefConflictInput> Conflicts);

public sealed record SuppliedBriefUnderstandingView(
    string? ClientName,
    string Title,
    string? CampaignMode,
    decimal CampaignModeConfidence,
    bool RequiresHumanClarification,
    string CampaignModeRationale,
    SuppliedBriefDraftView Draft,
    IReadOnlyList<SuppliedBriefQuestionView> Questions,
    IReadOnlyList<SuppliedBriefEvidenceView> Evidence,
    SuppliedBriefAgentUsageView Usage);

public sealed record SuppliedBriefAgentInput(
    Guid TenantId,
    Guid ActorId,
    string SourceTitle,
    string SourceContent,
    IReadOnlyList<BriefClarificationInput> Clarifications);

public interface ISuppliedBriefAgentClient
{
    Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input,
        CancellationToken cancellationToken);
}

public interface ISuppliedBriefUnderstandingService
{
    Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        ActorId actorId,
        TenantId tenantId,
        UnderstandSuppliedBriefRequest request,
        CancellationToken cancellationToken);
}
