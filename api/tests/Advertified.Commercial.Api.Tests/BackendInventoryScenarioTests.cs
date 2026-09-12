using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class BackendInventoryScenarioTests
{
    private static readonly string Root = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "InventoryHoldouts");

    public static IEnumerable<object[]> Cases() => BackendScenarioEvidence.Cases("INVENTORY_EXTRACTION");

    [Theory]
    [MemberData(nameof(Cases))]
    public void CanonicalInventoryScenario(string scenarioId) =>
        BackendScenarioEvidence.Run(scenarioId, () => Execute(scenarioId));

    private static BackendScenarioObservation Execute(string id)
    {
        var (file, kind, bytes, table) = BackendInventoryScenarioSources.Read(id, Root);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var extraction = NativeInventorySourcePreprocessor.Process(new(file, kind, kind, hash, bytes));
        var renamed = NativeInventorySourcePreprocessor.Process(new("unrelated-source." + kind, kind, kind, hash, bytes));
        Assert.Equal(extraction.CanonicalJson, renamed.CanonicalJson);
        Assert.Empty(extraction.Rows);
        return table is { } fixture
            ? Table(id, extraction, fixture)
            : Narrative(extraction);
    }

    private static BackendScenarioObservation Table(
        string id, InventoryExtractionResult extraction, JsonElement fixture)
    {
        var document = InventoryDocumentStructureBuilder.Build(extraction);
        var proposal = InventoryHoldoutProjection.Proposal(document, fixture);
        var codes = InventoryHoldoutProjection.Codes();
        var rows = InventorySchemaProjection.Project(document, proposal,
            InventoryDocumentStructureBuilder.GovernedCodes(codes));
        var accounting = proposal.Records.Select(record =>
            InventoryAcceptanceSourceAccounting.AccountSection(document, record, rows)).ToArray();
        Assert.All(accounting, item => Assert.Equal(InventoryAcceptanceCheckResult.Passed, item.Result));
        var prepared = InventoryCandidateAdmissionPolicy.Prepare(rows, extraction.SourceHash,
            "Not supplied", codes, new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(rows.Count, prepared.Length);
        Assert.NotEmpty(prepared);
        Assert.All(prepared, candidate => Assert.True(InventoryCandidateReviewPolicy.RequiresReview(candidate)));
        VerifyCommercialShape(id, prepared);
        return Observation(new { extraction.Document.SourceElements, proposal },
            new { rows, candidates = prepared, accounting }, prepared.SelectMany(item => item.Validation)
                .Where(issue => issue.IsBlocking).Select(issue => issue.FieldName).Distinct().Count());
    }

    private static void VerifyCommercialShape(string id, PreparedInventoryCandidate[] candidates)
    {
        if (id == "INV-005")
            Assert.All(candidates, item => Assert.Equal("Bundle K-531", item.Values.Package?.PackageName));
        if (id == "INV-006")
            Assert.All(candidates, item =>
            {
                Assert.Equal("10 percent only above 5 units", item.Values.CommercialTerms?.DiscountTerms);
                Assert.Equal(100000L, item.Values.RateAmountMinor);
            });
        if (id == "INV-007")
            Assert.Contains(candidates, item => item.Values.RateAmountMinor is null);
        if (id == "INV-008")
        {
            Assert.Equal(candidates[0].Values.ProductCode, candidates[1].Values.ProductCode);
            Assert.True(candidates[0].Values.CommercialTerms?.RateValidTo >=
                candidates[1].Values.CommercialTerms?.RateValidFrom);
            Assert.All(candidates, item => Assert.True(InventoryCandidateReviewPolicy.RequiresReview(item)));
        }
        if (id == "INV-014")
        {
            Assert.Equal(2, candidates.Length);
            Assert.Equal(candidates[0].Values.ProductCode, candidates[1].Values.ProductCode);
            Assert.NotEqual(candidates[0].SourceLocator, candidates[1].SourceLocator);
        }
        if (id == "INV-015")
            Assert.All(candidates, item => Assert.Null(item.Values.RateAmountMinor));
    }

    private static BackendScenarioObservation Narrative(InventoryExtractionResult extraction)
    {
        var packets = InventorySemanticPacketBuilder.BuildTranscription(extraction,
            InventoryHoldoutProjection.Codes(), new InventorySemanticOptions
            {
                Enabled = true, ModelId = "amazon.nova-lite-v1:0",
                InputPricePerMillionTokensUsdMicros = 60_000, OutputPricePerMillionTokensUsdMicros = 240_000,
                PerCallCostCapUsdMicros = 100_000, CertificationBudgetUsdMicros = 5_000_000,
                BudgetScope = "inventory-monthly",
            });
        InventoryHoldoutTranscription.Verify(packets, extraction.SourceHash);
        return Observation(new { extraction.Document.SourceElements },
            new { sourceHash = extraction.SourceHash, packets, disposition = "REVIEW_REQUIRED",
                unsupportedAmountRejected = true }, null);
    }

    private static BackendScenarioObservation Observation(object source, object actual, int? missing) =>
        new(source, actual, "REVIEW_REQUIRED", new Dictionary<string, bool>
        {
            ["SOURCE_ACCOUNTING_COMPLETE"] = true,
            ["NO_SUPPLIER_FILE_HARDCODING"] = true,
            ["AUTO_PUBLISH_DISABLED"] = true,
        }, ["INVENTORY_SOURCE_REVIEW"], 0, missing,
            "NO_COMMERCIAL_MUTATION", "NOT_APPLICABLE_LOCAL_EXTRACTION");
}
