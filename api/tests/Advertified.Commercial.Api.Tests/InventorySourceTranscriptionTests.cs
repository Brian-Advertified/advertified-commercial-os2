using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void UnresolvedImageOnlySourceCreatesNoBedrockPacket()
    {
        var request = new InventoryExtractionRequest(
            "unresolved-image.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            new string('f', 64),
            ImageOnlySpreadsheet(1));
        var extraction = NativeOfficeInventoryProjection.Apply(
            request,
            InventoryExtractionContract.Create(
                "docling",
                "docling-test",
                "3.0.0",
                request.SourceHash,
                "{}",
                []));

        var packets = InventorySemanticPacketBuilder.BuildEnrichment(
            request,
            extraction,
            EmptyCodes(),
            SemanticSettings());

        Assert.True(NativeOfficeImageReader.IsRequired(extraction.Rows));
        Assert.Empty(packets);
    }

    [Fact]
    public void DeterministicRowsAreEnrichedOnceWithoutImages()
    {
        var sourceHash = new string('e', 64);
        var request = new InventoryExtractionRequest(
            "DMS Digital Rate Card.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            sourceHash,
            DmsRateCardSpreadsheet());
        var rows = NativeSpreadsheetProjection.Read(request);
        var extraction = InventoryExtractionContract.Create(
            "docling",
            InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            sourceHash,
            "{}",
            rows);
        var empty = new HashSet<string>(StringComparer.Ordinal);
        var codes = new InventoryCodeSets(
            new HashSet<string>(
                [MasterDataCodes.Channels.Digital],
                StringComparer.Ordinal),
            new HashSet<string>(
                [MasterDataCodes.InventoryProductTypes.DigitalPlacement],
                StringComparer.Ordinal),
            empty,
            new HashSet<string>(
                [MasterDataCodes.Currencies.Zar],
                StringComparer.Ordinal),
            empty,
            empty,
            empty,
            empty,
            empty);

        var packets = InventorySemanticPacketBuilder.BuildEnrichment(
            request,
            extraction,
            codes,
            SemanticSettings());

        var packet = Assert.Single(packets);
        Assert.Equal(
            InventorySemanticOperations.SemanticEnrichment,
            packet.Operation);
        Assert.Empty(packet.Images);
        Assert.Equal(4, packet.ExistingRows.Count);
        Assert.Equal(
            4,
            packet.ExistingRows.Select(row => row.Locator)
                .Distinct(StringComparer.Ordinal).Count());
        var target = packet.ExistingRows[0];
        var response = SemanticResponse(new(
            [
                new ProposedInventoryCandidate(
                    target.Locator,
                    [
                        new ProposedInventoryField(
                            "channel",
                            target.Values["platform"],
                            MasterDataCodes.Channels.Digital,
                            target.Locator,
                            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
                            MasterDataCodes.InventoryTransformationTypes
                                .DerivedFromSourceContext,
                            0.99m),
                        new ProposedInventoryField(
                            "product_type",
                            target.Values["adunit"],
                            MasterDataCodes.InventoryProductTypes
                                .DigitalPlacement,
                            target.Locator,
                            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
                            MasterDataCodes.InventoryTransformationTypes
                                .DerivedFromChannel,
                            0.99m),
                    ],
                    []),
            ],
            []));

        var enriched = InventorySemanticMerger.Merge(
            rows,
            [packet],
            [new AgentSemanticResult(packet.InputHash, response)],
            codes);

        Assert.Equal(
            MasterDataCodes.Channels.Digital,
            enriched.Single(row => row.Locator == target.Locator)
                .Values["channel"]);
        Assert.Equal(
            1,
            enriched.Count(row => row.Values.ContainsKey("channel")));
        Assert.Equal(4, enriched.Count);
    }

    [Fact]
    public void EnrichmentBudgetFailsClosed()
    {
        var settings = new InventorySemanticOptions
        {
            PerCallCostCapUsdMicros = 30_000,
            CertificationBudgetUsdMicros = 100_000,
        };

        InventorySemanticBudgetPolicy.Ensure(
            [BudgetPacket(30_000), BudgetPacket(30_000), BudgetPacket(30_000)],
            settings);
        Assert.Throws<InventorySemanticBudgetExceededException>(() =>
            InventorySemanticBudgetPolicy.Ensure(
                [BudgetPacket(30_001)],
                settings));
        Assert.Throws<InventorySemanticBudgetExceededException>(() =>
            InventorySemanticBudgetPolicy.Ensure(
                [
                    BudgetPacket(30_000),
                    BudgetPacket(30_000),
                    BudgetPacket(30_000),
                    BudgetPacket(30_000),
                ],
                settings));
    }

    private static InventorySemanticOptions SemanticSettings() => new()
    {
        InputPricePerMillionTokensUsdMicros = 1,
        OutputPricePerMillionTokensUsdMicros = 1,
        BudgetScope = "inventory-test",
    };

    private static InventorySemanticPacket BudgetPacket(
        long maximumCostUsdMicros) => new(
        Guid.NewGuid(),
        InventorySemanticOperations.SemanticEnrichment,
        1,
        1,
        new string('a', 64),
        "{}",
        [],
        [],
        [],
        maximumCostUsdMicros);

    private static AgentRuntimeResponse<
        InventorySemanticExtractionArtifact> SemanticResponse(
            InventorySemanticExtractionArtifact artifact) => new()
        {
            SchemaVersion = "1.0.0",
            Status = MasterDataCodes.LifecycleStatuses.ReviewRequired,
            Artifact = artifact,
            EvidenceBindings = [],
            Unknowns = [],
            Assumptions = [],
            Confidence = [],
            Objections = [],
            Rationale = "Semantic enrichment requires review.",
            Usage = new AgentProviderUsage(
                "bedrock", "fixture", 1, 0, 0, "LIVE"),
        };
}
