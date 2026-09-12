using System.Security.Cryptography;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryUnseenHoldoutTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "InventoryHoldouts");

    public static IEnumerable<object[]> Cases() =>
        Manifest().GetProperty("cases").EnumerateArray()
            .Select(item => new object[] { item.GetProperty("file").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void UnfamiliarFilesPreserveEvidenceAndNeverInventCommercialReadiness(string file)
    {
        var fixture = Manifest().GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("file").GetString() == file);
        var bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, file));
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Equal(fixture.GetProperty("sha256").GetString(), hash);
        var kind = fixture.GetProperty("format").GetString()!;
        var extraction = Process(file, kind, hash, bytes);
        // An unrelated filename cannot provide supplier ownership or commercial meanings.
        var renamed = Process("unrelated-owner-name." + kind.ToLowerInvariant(), kind, hash, bytes);
        Assert.Equal(extraction.CanonicalJson, renamed.CanonicalJson);
        Assert.Empty(extraction.Rows);
        Assert.NotEmpty(extraction.Document.SourceElements!);
        Assert.All(extraction.Document.SourceElements!, element =>
            Assert.False(string.IsNullOrWhiteSpace(element.Locator)));
        if (fixture.TryGetProperty("tables", out _))
            VerifyTable(fixture, extraction);
        else
            VerifyTranscription(fixture, extraction);
    }

    [Fact]
    public void RetainedRowSchemaReplaysWithoutChangingCanonicalBytes()
    {
        const string retained = """{"sourceStructure":"s1","recordBoundary":{"firstRow":2,"lastRow":2,"rowsPerRecord":1,"excludedRows":[]},"fieldMappings":[],"supplierMetadataMappings":[],"assetMappings":[]}""";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var record = JsonSerializer.Deserialize<InventoryRecordSchema>(retained, options)!;
        Assert.Null(record.RecordAxis);
        Assert.Equal(retained, JsonSerializer.Serialize(record, options));
        var source = new InventorySourceStructure("s1", "cell", [new("A2", 2, 1, "UNSEEN-368")]);
        Assert.Same(source, InventoryRecordOrientation.Apply(source, record));
    }

    [Theory]
    [InlineData("DIAGONAL")]
    [InlineData("")]
    public void UnsupportedRecordAxisCannotAcquireProjectionAuthority(string axis)
    {
        var fixture = Manifest().GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("file").GetString() == "xlsx_transposed.xlsx");
        var bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, "xlsx_transposed.xlsx"));
        var extraction = Process("arbitrary.xlsx", "XLSX", fixture.GetProperty("sha256").GetString()!, bytes);
        var document = InventoryDocumentStructureBuilder.Build(extraction);
        var proposal = InventoryHoldoutProjection.Proposal(document, fixture);
        proposal = proposal with { Records = [proposal.Records[0] with { RecordAxis = axis }] };
        Assert.Throws<InventorySchemaRejectedException>(() =>
            InventorySchemaProjection.Project(document, proposal,
                InventoryDocumentStructureBuilder.GovernedCodes(InventoryHoldoutProjection.Codes())));
    }

    private static void VerifyTable(JsonElement fixture, InventoryExtractionResult extraction)
    {
        Assert.True(InventoryDocumentStructureBuilder.CanDiscover(extraction));
        var document = InventoryDocumentStructureBuilder.Build(extraction);
        VerifyPhysicalCells(fixture, document);
        var proposal = InventoryHoldoutProjection.Proposal(document, fixture);
        var codes = InventoryHoldoutProjection.Codes();
        var rows = InventorySchemaProjection.Project(document, proposal,
            InventoryDocumentStructureBuilder.GovernedCodes(codes));
        var expectedCount = proposal.Records.Sum(record =>
            (record.RecordBoundary.LastRow - record.RecordBoundary.FirstRow) /
            record.RecordBoundary.RowsPerRecord + 1);
        Assert.Equal(expectedCount, rows.Count);
        foreach (var record in proposal.Records)
            Assert.Equal(InventoryAcceptanceCheckResult.Passed,
                InventoryAcceptanceSourceAccounting.AccountSection(document, record, rows).Result);
        VerifySourceFields(document, rows);
        var prepared = InventoryCandidateAdmissionPolicy.Prepare(rows, extraction.SourceHash,
            "Not supplied", codes, new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(expectedCount, prepared.Length);
        for (var index = 0; index < rows.Count; index++)
            VerifyCommercialValues(rows[index], prepared[index]);
        Assert.All(prepared, candidate =>
        {
            Assert.True(InventoryCandidateReviewPolicy.RequiresReview(candidate));
            Assert.Equal(MasterDataCodes.AvailabilityStatuses.PlanningAvailable, candidate.Values.Availability);
            var availability = Assert.Single(candidate.Evidence, field => field.FieldName == "availability");
            Assert.Null(availability.RawValue);
            Assert.Equal(MasterDataCodes.InventoryEvidenceBases.DerivedPolicy, availability.EvidenceBasis);
            Assert.Equal(MasterDataCodes.InventoryExtractionMethods.PolicyDefault, availability.ExtractionMethod);
            Assert.Equal("policy:inventory-availability-default-v1", availability.SourceLocator);
            Assert.Null(candidate.Values.CommercialTerms?.RateValidFrom);
            Assert.Null(candidate.Values.CommercialTerms?.RateValidTo);
            Assert.Null(candidate.Values.CommercialTerms?.BookingDeadline);
        });
        Assert.All(rows, row => Assert.Null(InventoryCandidateNormalizer.Normalize(
            row, extraction.SourceHash, new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero)).SupplierName));
        if (fixture.GetProperty("file").GetString() == "xlsx_rate_variants.xlsx")
            Assert.All(prepared, candidate =>
            {
                Assert.Null(candidate.Values.RateAmountMinor);
                Assert.True(candidate.Values.Extension!.ContainsKey(
                    InventoryDiscoveredCandidateNormalizer.UnresolvedMarker));
            });
    }

    private static void VerifyCommercialValues(
        InventoryExtractedRow row, PreparedInventoryCandidate candidate)
    {
        var code = Assert.Single(row.DiscoveredFields!, field => field.CanonicalMeaning == "product_code");
        Assert.Equal(code.RawValue, candidate.Values.ProductCode);
        var rates = row.DiscoveredFields!.Where(field => field.CanonicalMeaning == "rate")
            .Select(field => field.RawValue).Distinct().ToArray();
        if (rates.Length == 1)
            Assert.Equal((long)(decimal.Parse(rates[0], System.Globalization.CultureInfo.InvariantCulture) * 100),
                candidate.Values.RateAmountMinor);
        else
            Assert.Null(candidate.Values.RateAmountMinor);
    }

    private static void VerifySourceFields(
        InventoryDocumentStructure document, IReadOnlyList<InventoryExtractedRow> rows)
    {
        var cells = document.Structures.SelectMany(structure => structure.Cells)
            .ToDictionary(cell => cell.Locator);
        foreach (var field in rows.SelectMany(row => row.DiscoveredFields!))
        {
            var source = cells[field.SourceLocator];
            Assert.Equal(source.RawText, field.RawValue);
            Assert.Equal(source.PositionJson, field.PositionJson);
        }
    }

    private static void VerifyPhysicalCells(JsonElement fixture, InventoryDocumentStructure document)
    {
        var tables = fixture.GetProperty("tables").EnumerateArray().ToArray();
        for (var section = 0; section < tables.Length; section++)
        {
            var matrix = tables[section].GetProperty("rows").EnumerateArray().ToArray();
            var cells = document.Structures[section].Cells;
            for (var row = 0; row < matrix.Length; row++)
            {
                var values = matrix[row].EnumerateArray().ToArray();
                for (var column = 0; column < values.Length; column++)
                {
                    var expected = values[column].GetString()!;
                    if (string.IsNullOrWhiteSpace(expected)) continue;
                    var actual = Assert.Single(cells, cell => cell.Row == row + 1 && cell.Column == column + 1);
                    Assert.Equal(expected, actual.RawText);
                }
            }
        }
    }

    private static void VerifyTranscription(JsonElement fixture, InventoryExtractionResult extraction)
    {
        Assert.False(InventoryDocumentStructureBuilder.CanDiscover(extraction));
        var packets = InventorySemanticPacketBuilder.BuildTranscription(extraction,
            InventoryHoldoutProjection.Codes(), new InventorySemanticOptions
            {
                Enabled = true, ModelId = "amazon.nova-lite-v1:0",
                InputPricePerMillionTokensUsdMicros = 60_000,
                OutputPricePerMillionTokensUsdMicros = 240_000,
                PerCallCostCapUsdMicros = 100_000,
                CertificationBudgetUsdMicros = 5_000_000,
                BudgetScope = "inventory-monthly",
            });
        Assert.NotEmpty(packets);
        var retained = string.Join("\n", extraction.Document.SourceElements!.Select(item => item.RawValue));
        foreach (var page in fixture.GetProperty("pages").EnumerateArray())
            foreach (var line in page.EnumerateArray())
                Assert.Contains(line.GetString()!, retained, StringComparison.Ordinal);
        Assert.All(packets, packet => Assert.Equal(
            InventorySemanticOperations.SourceTranscription, packet.Operation));
        InventoryHoldoutTranscription.Verify(packets, extraction.SourceHash);
        // Source proposals do not grant publication authority.
        Assert.Empty(extraction.Rows);
    }

    private static InventoryExtractionResult Process(
        string file, string kind, string hash, byte[] bytes) =>
        NativeInventorySourcePreprocessor.Process(new(file,
            "application/octet-stream", kind, hash, bytes));

    private static JsonElement Manifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json"))).RootElement;
}
