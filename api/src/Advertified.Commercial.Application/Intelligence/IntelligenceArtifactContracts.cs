namespace Advertified.Commercial.Application.Intelligence;

public sealed record ReferenceObservationFact(
    Guid ObservationId,
    string SourceTitle,
    string MeasurementPeriod,
    string GeographyLevel,
    string GeographyCode,
    string GeographyName,
    IReadOnlyDictionary<string, string> Dimensions,
    string MetricCode,
    decimal MetricValue,
    string MetricUnit,
    string StabilityCode,
    string SensitivityCode,
    string ActivationPolicy,
    IReadOnlyList<string> EvidenceNotes);

public sealed record IntelligenceArtifactDependencyInput(
    string ResourceType,
    Guid ResourceId,
    long ResourceVersion,
    string PurposeCode);

public sealed record IntelligenceArtifactEvidenceInput(
    string FieldPath,
    string ClassificationCode,
    Guid? EvidenceItemId,
    Guid? ReferenceObservationId,
    string? Rationale);

public sealed record IntelligenceInvocationUsage(
    string OperationCode,
    string Provider,
    string Model,
    long IncrementalCostMinor,
    string CacheStatus,
    string? ProviderRequestId,
    long InputTokens,
    long OutputTokens,
    long IncrementalCostUsdMicros);

public sealed record IntelligenceArtifactDraft(
    string SubjectType,
    Guid SubjectId,
    long SubjectVersion,
    string ServiceCode,
    string ArtifactSchemaVersion,
    string ArtifactJson,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string InputHash,
    IReadOnlyList<IntelligenceInvocationUsage> Invocations,
    IReadOnlyList<IntelligenceArtifactDependencyInput> Dependencies,
    IReadOnlyList<IntelligenceArtifactEvidenceInput> Evidence);

public sealed record IntelligenceArtifactView(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    long SubjectVersion,
    string ServiceCode,
    string ArtifactSchemaVersion,
    int VersionNumber,
    string ArtifactJson,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string InputHash,
    IReadOnlyList<IntelligenceInvocationUsage> Invocations,
    long TotalIncrementalCostMinor,
    string Status,
    Guid? SupersedesArtifactId,
    Guid CreatedBy,
    Guid? ApprovedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    long Version);
