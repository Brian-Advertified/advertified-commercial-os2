using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySemanticPreflightReader
{
    private static InventorySemanticSourcePreflightView ToView(
        PlannedSemanticSource plan,
        IReadOnlyList<SemanticPreflightRunRow> runs,
        long perCallCapUsdMicros)
    {
        var byHash = runs.ToDictionary(
            run => run.InputHash, StringComparer.Ordinal);
        var fresh = plan.Packets
            .Where(packet => !byHash.ContainsKey(packet.InputHash))
            .Sum(packet => packet.MaximumCostUsdMicros);
        var blocker = SourceBlocker(
            plan, byHash, perCallCapUsdMicros);
        return new(
            plan.Source.ImportId,
            plan.Source.InputArtifactId,
            plan.Source.FileName,
            plan.Source.SourceHash,
            plan.Source.DocumentClass ?? string.Empty,
            plan.Source.ImportStatus,
            plan.Source.SafeToReproject,
            plan.Packets.Count,
            plan.Packets.Sum(packet => packet.Images.Count),
            plan.Packets.Sum(packet => packet.SourceItems.Count),
            plan.Packets.Sum(
                packet => packet.MaximumCostUsdMicros),
            fresh,
            plan.Packets
                .Select(packet => packet.MaximumCostUsdMicros)
                .DefaultIfEmpty(0)
                .Max(),
            blocker);
    }

    private static string? SourceBlocker(
        PlannedSemanticSource plan,
        Dictionary<string,
            SemanticPreflightRunRow> runs,
        long perCallCapUsdMicros)
    {
        if (plan.Blocker is not null)
            return plan.Blocker;
        if (!plan.Source.SafeToReproject)
            return "REPROJECTION_NOT_ELIGIBLE";
        if (plan.Packets.Any(packet =>
                packet.MaximumCostUsdMicros >
                perCallCapUsdMicros))
            return "PER_CALL_COST_CAP_EXCEEDED";
        return plan.Packets.Any(packet =>
            runs.TryGetValue(packet.InputHash, out var run) &&
            run.Status is not (
                MasterDataCodes.LifecycleStatuses.Completed or
                MasterDataCodes.LifecycleStatuses.Pending))
            ? "SEMANTIC_RECONCILIATION_REQUIRED"
            : null;
    }

    private static string[] ReleaseBlockers(
        List<string> configurationBlockers,
        InventorySemanticSourcePreflightView[] sources,
        SemanticBudgetPreflight budget,
        InventorySemanticOptions settings)
    {
        var result = configurationBlockers.ToList();
        if (sources.Length == 0)
            result.Add("NO_ELIGIBLE_RETAINED_SOURCES");
        result.AddRange(sources
            .Select(source => source.Blocker)
            .Where(blocker => blocker is not null)
            .Select(blocker => blocker!));
        if (budget.ExistingCommittedUsdMicros +
            budget.NewMaximumUsdMicros >
            settings.CertificationBudgetUsdMicros)
            result.Add("CERTIFICATION_BUDGET_EXCEEDED");
        return result.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static List<string> ConfigurationBlockers(
        InventorySemanticOptions settings,
        AgentRuntimeOptions runtime)
    {
        var result = new List<string>();
        if (!InventorySemanticOptions.IsPlanningValid(settings))
            result.Add("SEMANTIC_PLAN_CONFIGURATION_INVALID");
        var routeConfigured =
            runtime.Mode == AgentRuntimeOptions.HttpMode &&
            runtime.Provider ==
                AgentRuntimeOptions.BedrockProvider;
        if (!routeConfigured)
            result.Add("BEDROCK_RUNTIME_NOT_ACTIVATED");
        else
        {
            if (!RuntimeModelMatches(settings, runtime))
                result.Add("BEDROCK_RUNTIME_MODEL_MISMATCH");
            if (!RuntimeCostCapMatches(settings, runtime))
                result.Add("BEDROCK_RUNTIME_COST_CAP_MISMATCH");
        }
        if (runtime.MaxAttempts != 1)
            result.Add("AMBIGUITY_SAFE_ATTEMPT_POLICY_REQUIRED");
        return result;
    }

    private static bool RuntimeModelMatches(
        InventorySemanticOptions settings,
        AgentRuntimeOptions runtime) =>
        string.Equals(
            runtime.ModelFor(
                MasterDataCodes.AgentTypes
                    .InventoryIntelligence),
            settings.ModelId,
            StringComparison.Ordinal);

    private static bool RuntimeCostCapMatches(
        InventorySemanticOptions settings,
        AgentRuntimeOptions runtime) =>
        runtime.CostCapFor(
            MasterDataCodes.AgentTypes.InventoryIntelligence) ==
        (settings.PerCallCostCapUsdMicros + 9_999L) /
        10_000L;

    private static bool IsLiveEnabled(
        InventorySemanticOptions settings,
        AgentRuntimeOptions runtime) =>
        settings.Enabled &&
        runtime.Mode == AgentRuntimeOptions.HttpMode &&
        runtime.Provider ==
            AgentRuntimeOptions.BedrockProvider &&
        runtime.AllowLive &&
        RuntimeModelMatches(settings, runtime) &&
        RuntimeCostCapMatches(settings, runtime);
}

internal sealed record PlannedSemanticSource(
    SemanticPreflightSourceRow Source,
    IReadOnlyList<InventorySemanticPacket> Packets,
    string? Blocker);

internal sealed record SemanticBudgetPreflight(
    IReadOnlyList<SemanticPreflightRunRow> Runs,
    long ExistingCommittedUsdMicros,
    long NewMaximumUsdMicros);

internal sealed record SemanticPreflightSourceRow
{
    public Guid ImportId { get; init; }
    public Guid InputArtifactId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public string? DocumentClass { get; init; }
    public string SourceHash { get; init; } = string.Empty;
    public string? ProtectedObjectKey { get; init; }
    public string ImportStatus { get; init; } = string.Empty;
    public string ProviderJson { get; init; } = string.Empty;
    public bool SafeToReproject { get; init; }
}

internal sealed record SemanticPreflightRunRow
{
    public string InputHash { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long MaximumCostUsdMicros { get; init; }
    public long ActualCostUsdMicros { get; init; }
}
