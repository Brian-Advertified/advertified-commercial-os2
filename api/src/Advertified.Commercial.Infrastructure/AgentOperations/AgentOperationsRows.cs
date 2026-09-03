namespace Advertified.Commercial.Infrastructure.AgentOperations;

internal sealed record AgentDefinitionRow
{
    public string AgentCode { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

internal sealed record AgentUsageSummaryRow
{
    public string AgentCode { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public long IncrementalCostMinor { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
}

internal sealed record AgentUsageRow
{
    public Guid Id { get; set; }
    public string AgentCode { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long? Units { get; set; }
    public int? ToolCalls { get; set; }
    public long IncrementalCostMinor { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed record AgentRunSummaryRow
{
    public int DurableRunCount { get; set; }
    public int AttentionRunCount { get; set; }
}

internal sealed record AgentOperationalRunRow
{
    public Guid Id { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? CampaignId { get; set; }
    public string RunKind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
    public long IncrementalCostMinor { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
