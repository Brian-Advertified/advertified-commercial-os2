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
    IReadOnlyList<string> EvidenceGaps);

public sealed record CampaignChannelCostView(string Channel, long SupplierCostMinor, long BudgetMinor);
