using System.Collections.Frozen;
using System.Text.Json;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Brief;

public static class SuppliedBriefFieldPaths
{
    public const string ClientName = "clientName";
    public const string Title = "title";
    public const string CampaignMode = "campaignMode";
    public const string BusinessProblem = "businessProblem";
    public const string Objective = "objective";
    public const string Audiences = "audiences";
    public const string Geographies = "geographies";
    public const string Timing = "timing";
    public const string Budget = "budget";
    public const string Currency = "currency";
    public const string VatStatus = "vatStatus";
    public const string Fees = "fees";
    public const string MediaRequirements = "mediaRequirements";
    public const string Constraints = "constraints";
    public static readonly string Measurement =
        JsonNamingPolicy.CamelCase.ConvertName(nameof(SuppliedBriefDraftView.Measurement));
    public const string Facts = "facts";
    public const string Unknowns = "unknowns";
    public const string Assumptions = "assumptions";
    public const string Conflicts = "conflicts";

    private static readonly FrozenSet<string> Supported = new[]
    {
        ClientName, Title, CampaignMode, BusinessProblem, Objective, Audiences,
        Geographies, Timing, Budget, Currency, VatStatus, Fees, MediaRequirements,
        Constraints, Measurement, Facts, Unknowns, Assumptions, Conflicts,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsSupported(string value) => Supported.Contains(value);
}

public sealed class SuppliedBriefInterpretationUnavailableException()
    : Exception("Supplied-brief interpretation is not configured.");

public sealed record UnderstandSuppliedBriefRequest(
    string SourceTitle,
    string SourceContent,
    IReadOnlyList<BriefClarificationInput>? Clarifications = null,
    Guid? InterpretationId = null,
    Guid? ParentInterpretationId = null);

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
    long IncrementalCostMinor,
    int Units = 0,
    string? CacheStatus = null,
    string? ProviderRequestId = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    long IncrementalCostUsdMicros = 0);

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
    SuppliedBriefAgentUsageView Usage,
    SuppliedBriefInterpretationReference? Interpretation = null);

public sealed record SuppliedBriefInterpretationReference(
    Guid Id, Guid? ParentId, int Version, string SourceHash);

public sealed record SuppliedBriefAgentInput(
    Guid TenantId,
    Guid ActorId,
    string SourceTitle,
    string SourceContent,
    IReadOnlyList<BriefClarificationInput> Clarifications,
    SuppliedBriefInterpretationReference? Interpretation = null);

public interface ISuppliedBriefAgentClient
{
    bool IsAvailable { get; }

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
