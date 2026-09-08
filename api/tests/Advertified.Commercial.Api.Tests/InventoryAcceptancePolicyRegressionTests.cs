using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryAcceptancePolicyRegressionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('b', 64);
    private static readonly InventoryCodeSets Codes = new(
        Set(MasterDataCodes.Channels.Digital),
        Set(MasterDataCodes.InventoryProductTypes.DigitalPlacement),
        Set(MasterDataCodes.RateTypes.FlatRate),
        Set(MasterDataCodes.Currencies.Zar),
        Set(MasterDataCodes.AvailabilityStatuses.PlanningAvailable),
        Set(), Set(), Set(), Set());

    [Fact]
    public void CompletePythonLineageCanPassAcceptance()
    {
        var candidate = Assert.Single(Evaluate(Fixture()));

        var decision = InventoryAcceptancePolicy.Read(candidate.Values)!;
        Assert.False(
            InventoryCandidateReviewPolicy.RequiresReview(candidate),
            JsonSerializer.Serialize(new
            {
                candidate.Validation,
                Decision = decision,
            }));
        Assert.Equal(
            MasterDataCodes.LifecycleStatuses.Approved,
            decision.Outcome);
        Assert.Contains(decision.Checks, check =>
            check.Check ==
                InventoryAcceptanceCheck.InterpretationBinding &&
            check.Result == InventoryAcceptanceCheckResult.Passed);
    }

    [Theory]
    [InlineData(InventoryAcceptanceCheckResult.Failed)]
    [InlineData(InventoryAcceptanceCheckResult.NotEvaluated)]
    [InlineData(InventoryAcceptanceCheckResult.NotApplicable)]
    public void RequiredEvidenceCannotBeOverridden(
        InventoryAcceptanceCheckResult result)
    {
        var candidate = Assert.Single(Evaluate(Fixture()));
        var evaluation =
            InventoryAcceptancePolicy.Read(candidate.Values)!;
        var checks = evaluation.Checks.Select(check =>
            check.Check == InventoryAcceptanceCheck.SourceIdentity
                ? check with { Result = result }
                : check).ToArray();

        Assert.False(InventoryAcceptancePolicy.Complete(checks));
        Assert.NotEqual(
            MasterDataCodes.LifecycleStatuses.Approved,
            InventoryAcceptancePolicy.Outcome(checks));
    }

    [Fact]
    public void UnreferencedCommercialTermRequiresReview()
    {
        var candidate = Assert.Single(Evaluate(Fixture(true)));

        Assert.True(
            InventoryCandidateReviewPolicy.RequiresReview(candidate));
        Assert.Contains(
            InventoryAcceptancePolicy.Read(candidate.Values)!.Checks,
            check =>
                check.Check ==
                    InventoryAcceptanceCheck.SourceContentAccounting &&
                check.Result == InventoryAcceptanceCheckResult.Failed);
    }

    [Fact]
    public void RetainedPythonLineageReplaysWithoutLegacySchema()
    {
        var extraction = Fixture();

        var replay = InventoryRetainedSchemaProjection.Replay(
            Hash,
            extraction.ProviderJson,
            extraction.Document,
            InventoryExtractionOptions.PinnedAdapterVersion);

        Assert.Single(replay.Rows);
        Assert.Null(replay.Document.DiscoveredSchema);
        Assert.Null(replay.Document.SchemaDiscoveryFailure);
    }

    [Fact]
    public void RetainedDocumentWithoutAnyLineageIsBlocked()
    {
        var extraction = Fixture();
        var document = extraction.Document with
        {
            SourceElements = [],
        };

        var replay = InventoryRetainedSchemaProjection.Replay(
            Hash,
            extraction.ProviderJson,
            document,
            InventoryExtractionOptions.PinnedAdapterVersion);

        Assert.Empty(replay.Rows);
        Assert.NotNull(replay.Document.SchemaDiscoveryFailure);
    }

    [Fact]
    public void MissingPythonSourceBindingRequiresReview()
    {
        var extraction = Fixture() with
        {
            Document = Fixture().Document with
            {
                SourceElements = [],
            },
        };

        var candidate = Assert.Single(Evaluate(extraction));

        Assert.True(
            InventoryCandidateReviewPolicy.RequiresReview(candidate));
        Assert.Contains(
            InventoryAcceptancePolicy.Read(candidate.Values)!.Checks,
            check =>
                check.Check ==
                    InventoryAcceptanceCheck.InterpretationBinding &&
                check.Result == InventoryAcceptanceCheckResult.Failed);
    }

    internal static PreparedInventoryCandidate[] Evaluate(
        InventoryExtractionResult extraction)
    {
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            extraction.Rows, Hash, "", Codes, Now);
        extraction = InventoryExtractionSourceAccounting.Attach(
            extraction, candidates);
        return InventoryAcceptancePolicy.Apply(
            extraction, Hash, 1, Codes, candidates, Now);
    }

    internal static InventoryExtractionResult Fixture(
        bool footnote = false,
        string? sourceHash = null,
        string? productCode = null,
        string? name = null)
    {
        var hash = sourceHash ?? Hash;
        var values = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["productcode"] = productCode ?? "unit-1",
            ["name"] = name ?? "Evidence product",
            ["channel"] = MasterDataCodes.Channels.Digital,
            ["producttype"] =
                MasterDataCodes.InventoryProductTypes.DigitalPlacement,
            ["geography"] = "Source region",
            ["rate"] = "R 100.00",
            ["currency"] = MasterDataCodes.Currencies.Zar,
            ["ratetype"] = MasterDataCodes.RateTypes.FlatRate,
        };
        var locators = values.Keys.ToDictionary(
            key => key,
            key => $"source:page=1;table=1;row=2;cell={key}",
            StringComparer.Ordinal);
        var elements = values.Select((item, index) =>
            new InventoryExtractedSourceElement(
                locators[item.Key],
                "source:page=1;table=1",
                "table",
                2,
                index + 1,
                item.Value)).ToList();
        if (footnote)
        {
            elements.Add(new InventoryExtractedSourceElement(
                "source:page=1;text=20",
                "source:page=1;text",
                "footnote",
                20,
                1,
                "Commercial rate condition"));
        }
        var row = new InventoryExtractedRow(
            1,
            locators["productcode"],
            values,
            "TABULAR",
            FieldLocators: locators);
        var provider = JsonSerializer.Serialize(new
        {
            tables = new[] { new { evidence = "retained" } },
        });
        return InventoryExtractionContract.Create(
            "source-extraction",
            InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            hash,
            provider,
            [row],
            sourceElements: elements);
    }

    private static HashSet<string> Set(
        params string[] values) =>
        new(values, StringComparer.Ordinal);
}
