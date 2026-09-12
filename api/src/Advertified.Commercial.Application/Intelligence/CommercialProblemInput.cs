namespace Advertified.Commercial.Application.Intelligence;

public sealed record CommercialProblemConflictInput(
    string FieldPath,
    string Description,
    string Severity,
    bool Resolved,
    string? Resolution);

public sealed record CommercialProblemInput(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid CorrelationId,
    Guid BriefVersionId,
    long BriefVersion,
    string ClientName,
    string BusinessProblem,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    IReadOnlyList<string> MediaRequirements,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<CommercialProblemConflictInput> Conflicts,
    IReadOnlyList<string> SuccessMeasures,
    long? BudgetMinor,
    string? Currency,
    IReadOnlyList<Guid> EvidenceItemIds);
