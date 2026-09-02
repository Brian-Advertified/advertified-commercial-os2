using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed record PlanningPolicy(
    string BenchmarkVersion,
    IReadOnlyList<decimal> OohRadiiKilometres,
    int MinimumBenchmarkCohort,
    int MediumBenchmarkCohort,
    int HighBenchmarkCohort,
    decimal LowBenchmarkConfidence,
    decimal MediumBenchmarkConfidence,
    decimal HighBenchmarkConfidence,
    bool AutomatedSelectionEnabled,
    string SuitabilityPolicyVersion,
    long MaximumAutomatedClientValueMinor,
    InventorySuitabilityWeights SuitabilityWeights,
    decimal DefaultRouteBufferMetres,
    decimal DefaultCoverageThreshold,
    decimal RegisteredVatRate,
    IReadOnlyDictionary<string, int> RateBillingDays)
{
    public static PlanningPolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var benchmark = RequireItem(
            registry,
            MasterDataCodes.PlanningPolicies.Collection,
            MasterDataCodes.PlanningPolicies.OohLocalPeerV1);
        var vat = RequireItem(
            registry,
            MasterDataCodes.VatStatuses.Collection,
            MasterDataCodes.VatStatuses.Registered);
        var selection = RequireItem(
            registry,
            MasterDataCodes.PlanningPolicies.Collection,
            MasterDataCodes.PlanningPolicies.InventorySuitabilityOohV1);
        using var benchmarkMetadata = JsonDocument.Parse(benchmark.MetadataJson);
        using var vatMetadata = JsonDocument.Parse(vat.MetadataJson);
        using var selectionMetadata = JsonDocument.Parse(selection.MetadataJson);
        var metadata = benchmarkMetadata.RootElement;
        var radii = metadata.GetProperty("radiiKilometres").EnumerateArray()
            .Select(item => item.GetDecimal()).ToArray();
        var minimum = metadata.GetProperty("minimumCohortSize").GetInt32();
        var medium = metadata.GetProperty("mediumCohortSize").GetInt32();
        var high = metadata.GetProperty("highCohortSize").GetInt32();
        var lowConfidence = metadata.GetProperty("lowConfidence").GetDecimal();
        var mediumConfidence = metadata.GetProperty("mediumConfidence").GetDecimal();
        var highConfidence = metadata.GetProperty("highConfidence").GetDecimal();
        var automatedSelectionEnabled = selectionMetadata.RootElement
            .GetProperty("automatedSelectionEnabled").GetBoolean();
        var selectionRoot = selectionMetadata.RootElement;
        var maximumAutomatedValue = selectionRoot
            .GetProperty("maximumAutomatedClientValueMinor").GetInt64();
        var weightsMetadata = selectionRoot.GetProperty("weights");
        var weights = new InventorySuitabilityWeights(
            weightsMetadata.GetProperty("geography").GetDecimal(),
            weightsMetadata.GetProperty("audienceContext").GetDecimal(),
            weightsMetadata.GetProperty("objectiveFormat").GetDecimal(),
            weightsMetadata.GetProperty("budgetEfficiency").GetDecimal(),
            weightsMetadata.GetProperty("evidenceQualityFreshness").GetDecimal(),
            weightsMetadata.GetProperty("portfolioCoverageDiversity").GetDecimal());
        var routeBuffer = selectionRoot.GetProperty("defaultRouteBufferMetres").GetDecimal();
        var coverageThreshold = selectionRoot.GetProperty("defaultCoverageThreshold").GetDecimal();
        var vatRate = vatMetadata.RootElement.GetProperty("rate").GetDecimal();
        ValidateBenchmarkPolicy(
            radii, minimum, medium, high,
            lowConfidence, mediumConfidence, highConfidence, vatRate);
        if (!automatedSelectionEnabled || maximumAutomatedValue <= 0 ||
            weights.Total != 1m || routeBuffer <= 0 ||
            coverageThreshold is <= 0 or > 1)
        {
            throw new InvalidOperationException(
                "The governed inventory suitability policy is invalid.");
        }
        return new(
            benchmark.Code, radii, minimum, medium, high,
            lowConfidence, mediumConfidence, highConfidence,
            automatedSelectionEnabled, selection.Code, maximumAutomatedValue,
            weights, routeBuffer, coverageThreshold,
            vatRate, LoadRateBillingDays(registry));
    }

    private static Dictionary<string, int> LoadRateBillingDays(
        MasterDataRegistry registry)
    {
        var collection = registry.Collections.Single(item =>
            item.Code == MasterDataCodes.RateTypes.Collection);
        return collection.Items
            .Select(item => (item.Code, Days: ReadBillingDays(item.MetadataJson)))
            .Where(item => item.Days.HasValue)
            .ToDictionary(item => item.Code, item => item.Days!.Value, StringComparer.Ordinal);
    }

    private static int? ReadBillingDays(string metadataJson)
    {
        using var metadata = JsonDocument.Parse(metadataJson);
        return metadata.RootElement.TryGetProperty("billingDays", out var value)
            ? value.GetInt32()
            : null;
    }

    private static void ValidateBenchmarkPolicy(
        decimal[] radii,
        int minimum,
        int medium,
        int high,
        decimal lowConfidence,
        decimal mediumConfidence,
        decimal highConfidence,
        decimal vatRate)
    {
        if (radii.Length == 0 || radii.Any(radius => radius <= 0) ||
            !radii.SequenceEqual(radii.Order()) ||
            minimum < 1 || medium < minimum || high < medium ||
            lowConfidence is < 0 or > 1 || mediumConfidence is < 0 or > 1 ||
            highConfidence is < 0 or > 1 ||
            lowConfidence > mediumConfidence || mediumConfidence > highConfidence ||
            vatRate is < 0 or > 1)
        {
            throw new InvalidOperationException("The canonical planning policy is invalid.");
        }
    }

    private static MasterDataRegistryItem RequireItem(
        MasterDataRegistry registry,
        string collection,
        string code) => registry.Collections
        .SingleOrDefault(item => item.Code == collection)?.Items
        .SingleOrDefault(item => item.Code == code && item.IsActive)
        ?? throw new InvalidOperationException(
            "The canonical planning policy is missing from master data.");
}

public sealed record InventorySuitabilityWeights(
    decimal Geography,
    decimal AudienceContext,
    decimal ObjectiveFormat,
    decimal BudgetEfficiency,
    decimal EvidenceQualityFreshness,
    decimal PortfolioCoverageDiversity)
{
    public decimal Total => Geography + AudienceContext + ObjectiveFormat +
        BudgetEfficiency + EvidenceQualityFreshness + PortfolioCoverageDiversity;
}
