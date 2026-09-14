using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryEligibilityEvaluatorTests
{
    [Fact]
    public void SocialInventoryUsesAudienceTargetingInsteadOfPlacementTextForGeography()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Social,
            "urban South African communities near relevant retail nodes");

        Assert.Equal(MasterDataCodes.RejectionReasons.MissingInfo, result.RejectionReason);
        Assert.NotEqual(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void PlaceBoundInventoryStillRequiresBriefGeographyMatch()
    {
        var result = Evaluate(MasterDataCodes.Channels.Ooh, "Botswana");

        Assert.Equal(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void SouthAfricaScopeAcceptsLocalPlaceBoundInventory()
    {
        var result = Evaluate(MasterDataCodes.Channels.Ooh, "South Africa");

        Assert.NotEqual(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void ExplicitBroaderMarketInInventoryNameCanSatisfyBriefGeography()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Radio,
            "Broader Gauteng",
            inventoryName: "Radio Pulpit 657 AM — SUNDAY 12:00-15:00 (Gauteng 1)",
            inventoryGeography: "Radio Pulpit 657 AM");

        Assert.NotEqual(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void KnownMetroCanSatisfyBroaderProvinceScopeWithoutDuplicatingGeographyMaps()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Ooh,
            "Broader Gauteng",
            inventoryName: "Local Demo Johannesburg Digital Billboard",
            inventoryGeography: "Johannesburg");

        Assert.NotEqual(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void StructuredProvinceCanSatisfyBroaderMarketWhenDisplayGeographyIsOpaque()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Ooh,
            "Broader Gauteng",
            inventoryName: "Opaque inventory code",
            inventoryGeography: "SITE-001",
            spatialJson: "{\"country\":\"South Africa\",\"province\":\"Gauteng\"}");

        Assert.NotEqual(MasterDataCodes.RejectionReasons.IneligibleGeography, result.RejectionReason);
    }

    [Fact]
    public void ExactDigitalLargeFormatConstraintRejectsInventoryWithoutFormatEvidence()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Dooh,
            "South Africa",
            ["Media: OOH and DOOH only. Use only digital large-format sites."]);

        Assert.Equal(MasterDataCodes.RejectionReasons.IneligibleFormat, result.RejectionReason);
        Assert.Contains("large-format evidence", result.RejectionDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void DigitalOnlyConstraintRejectsStaticOoh()
    {
        var result = Evaluate(
            MasterDataCodes.Channels.Ooh,
            "South Africa",
            ["Media: Use only digital sites."]);

        Assert.Equal(MasterDataCodes.RejectionReasons.IneligibleFormat, result.RejectionReason);
    }

    private static EligibilityResult Evaluate(
        string channel,
        string requestedGeography,
        IReadOnlyList<string>? constraints = null,
        string? inventoryName = null,
        string? inventoryGeography = null,
        string? spatialJson = null)
    {
        var inventory = new PlanningInventoryRow(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Fixture supplier", inventoryName ?? "Social package", channel, "SOCIAL_PLACEMENT",
            inventoryGeography ?? "Social package",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, "[]", null, "REGISTERED", null, "EXCLUSIVE",
            null, null, spatialJson, null);
        var allocation = new MediaAllocationView(
            channel, 100_000_00, "Targeted reach",
            [new MediaRunningPeriodView(
                new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31))]);
        var allocations = new Dictionary<string, MediaAllocationView>(
            StringComparer.Ordinal)
        {
            [channel] = allocation,
        };

        return InventoryEligibilityEvaluator.Evaluate(
            inventory,
            [requestedGeography],
            constraints ?? [],
            allocations,
            "ZAR",
            PlanningPolicy.Load());
    }
}
