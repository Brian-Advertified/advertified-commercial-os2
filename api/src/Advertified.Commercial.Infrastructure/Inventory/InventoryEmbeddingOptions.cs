namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryEmbeddingOptions
{
    public const string SectionName = "InventoryEmbedding";
    public const string DisabledMode = "Disabled";
    public const string DeterministicMode = "Deterministic";
    public const string BedrockHttpMode = "BedrockHttp";
    public const string TitanModel = "amazon.titan-embed-text-v2:0";
    public const string BedrockRegion = "eu-west-1";
    public const int Dimensions = 1024;
    public const long StagingBudgetUsdMicros = 3_000_000;
    public const long ProductionBudgetUsdMicros = 10_000_000;

    public string Mode { get; init; } = DisabledMode;
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public string? ServiceKey { get; init; }
    public string Model { get; init; } = TitanModel;
    public string Region { get; init; } = BedrockRegion;
    public int OutputDimensions { get; init; } = Dimensions;
    public bool Normalize { get; init; } = true;
    public bool AllowLive { get; init; }
    public long MonthlyBudgetUsdMicros { get; init; } = StagingBudgetUsdMicros;
    public long MaximumRequestCostUsdMicros { get; init; } = 10_000;

    public static bool IsValid(InventoryEmbeddingOptions value) =>
        value.Mode is DisabledMode or DeterministicMode or BedrockHttpMode &&
        value.Model == TitanModel && value.Region == BedrockRegion &&
        value.OutputDimensions == Dimensions && value.Normalize &&
        value.MonthlyBudgetUsdMicros is > 0 and <= ProductionBudgetUsdMicros &&
        value.MaximumRequestCostUsdMicros >= 0 &&
        value.MaximumRequestCostUsdMicros <= value.MonthlyBudgetUsdMicros &&
        (value.Mode != BedrockHttpMode || value.AllowLive &&
            Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(value.ServiceKey));
}
