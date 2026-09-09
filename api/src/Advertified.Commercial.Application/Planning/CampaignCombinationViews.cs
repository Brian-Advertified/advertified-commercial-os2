namespace Advertified.Commercial.Application.Planning;

public sealed record CampaignCombinationsView(
    IReadOnlyList<CampaignCombinationView> Alternatives,
    bool SearchTruncated,
    int CandidatesConsidered,
    int MissingCostCandidateCount);

public sealed record CampaignCombinationView(
    IReadOnlyList<Guid> CandidateIds,
    long CampaignSupplierCostMinor,
    string Currency,
    IReadOnlyList<CampaignChannelCostView> ChannelCosts,
    IReadOnlyList<Guid> CoveredRequirementIds,
    IReadOnlyList<string> EvidenceGaps,
    CampaignRelativeComparisonView? Comparison = null,
    CampaignAudienceForecastView? AudienceForecast = null,
    CampaignScenarioView? Scenario = null);

public sealed record CampaignScenarioView(
    string Code,
    bool Recommended,
    long SupplierCostDeltaMinor,
    decimal? DeduplicatedReachDelta,
    decimal? AverageFrequencyDelta);

public sealed record CampaignAudienceForecastView(
    decimal? GrossReach,
    decimal? DeduplicatedReach,
    decimal? DuplicatedReach,
    decimal? TotalImpressions,
    decimal? AverageFrequency,
    string? Universe,
    string? MeasurementPeriod,
    string? MeasurementSource,
    string? Methodology,
    IReadOnlyList<CampaignCandidateIncrementalReachView> IncrementalReach,
    IReadOnlyList<string> EvidenceGaps);

public sealed record CampaignCandidateIncrementalReachView(
    Guid CandidateId,
    decimal? IncrementalReach,
    string? EvidenceGap);

public sealed record CampaignRelativeComparisonView(
    long SupplierCostDeltaMinor,
    IReadOnlyList<Guid> AddedCandidateIds,
    IReadOnlyList<Guid> RemovedCandidateIds,
    int MeasuredTargetCandidateCount,
    int MissingDeliveryCandidateCount,
    int DistinctInventoryWorkspaceCount,
    IReadOnlyList<string> PlannedChannelRoles);

public sealed record CampaignChannelCostView(string Channel, long SupplierCostMinor, long BudgetMinor);
