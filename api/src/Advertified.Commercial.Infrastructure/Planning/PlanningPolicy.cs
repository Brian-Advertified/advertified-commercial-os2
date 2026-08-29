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
    decimal EligibilityScoreBase,
    decimal EligibilityPriceWeight,
    decimal EligibilityAvailableSupplyBonus,
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
        using var benchmarkMetadata = JsonDocument.Parse(benchmark.MetadataJson);
        using var vatMetadata = JsonDocument.Parse(vat.MetadataJson);
        var metadata = benchmarkMetadata.RootElement;
        var radii = metadata.GetProperty("radiiKilometres").EnumerateArray()
            .Select(item => item.GetDecimal()).ToArray();
        var minimum = metadata.GetProperty("minimumCohortSize").GetInt32();
        var medium = metadata.GetProperty("mediumCohortSize").GetInt32();
        var high = metadata.GetProperty("highCohortSize").GetInt32();
        var lowConfidence = metadata.GetProperty("lowConfidence").GetDecimal();
        var mediumConfidence = metadata.GetProperty("mediumConfidence").GetDecimal();
        var highConfidence = metadata.GetProperty("highConfidence").GetDecimal();
        var scoreBase = metadata.GetProperty("scoreBase").GetDecimal();
        var priceWeight = metadata.GetProperty("priceWeight").GetDecimal();
        var supplyBonus = metadata.GetProperty("availableSupplyBonus").GetDecimal();
        var vatRate = vatMetadata.RootElement.GetProperty("rate").GetDecimal();
        ValidateBenchmarkPolicy(
            radii, minimum, medium, high,
            lowConfidence, mediumConfidence, highConfidence,
            scoreBase, priceWeight, supplyBonus, vatRate);
        return new(
            benchmark.Code, radii, minimum, medium, high,
            lowConfidence, mediumConfidence, highConfidence,
            scoreBase, priceWeight, supplyBonus,
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
        decimal scoreBase,
        decimal priceWeight,
        decimal supplyBonus,
        decimal vatRate)
    {
        if (radii.Length == 0 || radii.Any(radius => radius <= 0) ||
            !radii.SequenceEqual(radii.Order()) ||
            minimum < 1 || medium < minimum || high < medium ||
            lowConfidence is < 0 or > 1 || mediumConfidence is < 0 or > 1 ||
            highConfidence is < 0 or > 1 ||
            lowConfidence > mediumConfidence || mediumConfidence > highConfidence ||
            scoreBase <= 0 || priceWeight < 0 || supplyBonus < 0 ||
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
