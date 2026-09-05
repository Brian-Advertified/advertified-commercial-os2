using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySchemaEvidenceRegressionTests
{
    private static readonly string SourceHash = new('a', 64);
    private static readonly string[] UnmappedRawValues = ["first raw", "second raw"];
    private static readonly DateTimeOffset Captured = new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlySet<string> Meanings = new HashSet<string> { "product_code", "rate" };
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Codes =
        new Dictionary<string, IReadOnlySet<string>>();

    [Fact]
    public void AddingOptionalSchemaEvidenceDoesNotChangeHistoricalCanonicalBytes()
    {
        const string historical = """
            {"schemaVersion":"fixture/1","rows":[{"number":1,"locator":"cell:1","values":{"name":"Original"},"extractionMethod":null,"confidence":null,"fieldLocators":null,"fieldConfidences":null,"fieldEvidenceBases":null,"fieldTransformations":null}]}
            """;
        var replay = InventoryExtractionContract.Replay(historical, "fixture/1");
        Assert.Equal(historical, InventoryExtractionContract.Serialize(replay));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DocumentCannotAssignSupplierOwnership(bool ownershipAssigned)
    {
        var source = new InventoryImportRow
        {
            SupplierId = ownershipAssigned ? Guid.NewGuid() : null,
            SupplierName = ownershipAssigned ? "Authenticated supplier" : string.Empty,
            SupplierIdentityEvidenceJson = "{\"basis\":\"authenticated_scope\"}",
        };
        var extraction = InventoryExtractionContract.Create("fixture", "1", "fixture/1", SourceHash, "{}",
            [new InventoryExtractedRow(1, "cell:1", new Dictionary<string, string>
            {
                ["supplier"] = "Different document supplier",
            })]);
        var result = InventorySupplierIdentityService.ResolveExtraction(source, extraction);
        Assert.Equal(source.SupplierId, result.SupplierId);
        Assert.False(result.SupplierCreated);
        if (ownershipAssigned)
        {
            Assert.Equal(source.SupplierName, result.SupplierName);
            Assert.Equal(source.SupplierIdentityEvidenceJson, result.EvidenceJson);
        }
        else Assert.Contains("Different document supplier", result.EvidenceJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneSchemaInterpretationProjectsTenThousandRecordsIncludingUnsampledRows()
    {
        var (document, schema) = Fixture(10_000);
        var interpreter = new FixtureInterpreter(schema);
        var discovered = await new InventorySchemaDiscoveryService(interpreter).DiscoverAsync(
            document, Meanings, Codes, CancellationToken.None);
        var rows = InventorySchemaBatchProjection.Project(document, discovered, Meanings, Codes);
        Assert.Equal(1, interpreter.Calls);
        Assert.True(interpreter.Request!.RepresentativeStructures[0].Cells.Count < document.Structures[0].Cells.Count);
        Assert.Equal(10_000, rows.Length);
        Assert.Equal("unit-5051", rows[5050].DiscoveredFields![0].RawValue);
        Assert.Equal("Nebular token", rows[5050].DiscoveredFields![0].RawLabel);
        Assert.Equal("row:5051;column:0", rows[5050].DiscoveredFields![0].SourceLocator);
        Assert.All(rows, row => Assert.Null(row.SchemaWarnings));
        // This fake proves the batching/evidence contract, not a model's ability to discover meaning.
        Assert.Equal(0, discovered.Provenance.AiCalls);
        Assert.Equal(0L, discovered.Provenance.CostUsdMicros);
    }

    [Fact]
    public void UnmappedRecordsRemainReviewCandidatesWithAllRawEvidenceAndNoGuessedIdentity()
    {
        var row = new InventoryExtractedRow(1, "row:1", new Dictionary<string, string>(),
            DiscoveredFields: [Field(null, "first raw", "cell:1"), Field(null, "second raw", "cell:2")]);
        var empty = new HashSet<string>();
        var codes = new InventoryCodeSets(empty, empty, empty, empty, empty, empty, empty, empty, empty);
        var candidate = Assert.Single(InventoryCandidateAdmissionPolicy.Prepare([row], SourceHash, "", codes, Captured));
        Assert.Null(candidate.Values.ProductCode);
        Assert.Null(candidate.Values.RateType);
        Assert.Contains(candidate.Validation, issue => issue.FieldName == "schema" && issue.IsBlocking);
        Assert.Equal(UnmappedRawValues, candidate.Evidence
            .Where(field => field.RawValue is not null).Select(field => field.RawValue));
        Assert.Equal(candidate.Evidence.Count, candidate.Evidence.Select(field => field.FieldName).Distinct().Count());
    }

    [Theory]
    [InlineData("rate", "R1,10", "R2,20")]
    [InlineData("availability", "AVAILABLE", "UNAVAILABLE")]
    public void ConflictingEvidenceRemainsDistinctAndCannotAcquireADefault(string meaning, string first, string second)
    {
        var row = new InventoryExtractedRow(1, "row:1", new Dictionary<string, string>(),
            DiscoveredFields: [Field(meaning, first, "cell:1"), Field(meaning, second, "cell:2")]);
        var candidate = InventoryCandidateNormalizer.Normalize(row, SourceHash, Captured);
        var raw = candidate.Evidence.Where(field => field.RawValue is not null).ToArray();
        Assert.Equal(2, raw.Length);
        Assert.All(raw, field => Assert.Null(field.NormalizedValue));
        Assert.Equal(candidate.Evidence.Count, candidate.Evidence.Select(field => field.FieldName).Distinct().Count());
        Assert.Null(candidate.Values.RateAmountMinor);
        if (meaning == "availability") Assert.Null(candidate.Values.Availability);
    }

    [Fact]
    public void MissingMappedCellsProduceWarningsWithoutFabricatingSourceEvidence()
    {
        var (document, schema) = Fixture(2);
        document = document with { Structures = [document.Structures[0] with
        {
            Cells = document.Structures[0].Cells.Where(cell => cell.Row != 2 || cell.Column != 1).ToArray(),
        }] };
        var rows = InventorySchemaBatchProjection.Project(document, schema, Meanings, Codes);
        Assert.Equal(2, rows.Length);
        Assert.Null(rows[0].SchemaWarnings);
        Assert.NotEmpty(rows[1].SchemaWarnings!);
        Assert.Single(rows[1].DiscoveredFields!);
        Assert.DoesNotContain(rows[1].DiscoveredFields!, field => field.CanonicalMeaning == "rate");
    }

    [Theory]
    [InlineData("latitude", "-25,75")]
    [InlineData("rate_valid_from", "03/04/2026")]
    [InlineData("rate_valid_from", "September 5")]
    public void AmbiguousCoordinatesAndDatesRetainRawEvidenceForReview(string meaning, string raw)
    {
        var row = new InventoryExtractedRow(1, "row:1", new Dictionary<string, string>(),
            DiscoveredFields: [Field(meaning, raw, "cell:1")]);
        var candidate = InventoryCandidateNormalizer.Normalize(row, SourceHash, Captured);
        var evidence = Assert.Single(candidate.Evidence, item => item.RawValue == raw);
        Assert.Null(evidence.NormalizedValue);
        Assert.Null(candidate.Values.Latitude);
        Assert.Null(candidate.Values.CommercialTerms?.RateValidFrom);
        Assert.True(candidate.Values.Extension!.ContainsKey(InventoryDiscoveredCandidateNormalizer.UnresolvedMarker));
    }

    [Fact]
    public void SchemaAndExactRawEvidenceSurviveCanonicalReplay()
    {
        var (document, schema) = Fixture(2);
        var rows = InventorySchemaBatchProjection.Project(document, schema, Meanings, Codes);
        var result = InventoryExtractionContract.Create("fixture", "1", "fixture/1", SourceHash, "{}", rows, schema);
        var replay = InventoryExtractionContract.Replay(result.CanonicalJson, "fixture/1");
        Assert.Equal(result.CanonicalJson, InventoryExtractionContract.Serialize(replay));
        Assert.Equal(schema.StructureHash, replay.DiscoveredSchema!.StructureHash);
        Assert.Equal("  100.00  ", replay.Rows[0].DiscoveredFields![1].RawValue);
    }

    private static InventoryDiscoveredField Field(string? meaning, string raw, string locator) =>
        new(meaning, "Unfamiliar label", raw, locator, "structure:1", null, "Fixture interpretation",
            1m, null, meaning is null ? ["Unresolved meaning"] : []);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetainedReplayUsesOnlyExistingSchemaOrReturnsDocumentReview(bool hasSchema)
    {
        var (document, schema) = Fixture(2);
        var rows = InventorySchemaBatchProjection.Project(document, schema, Meanings, Codes);
        var retained = new InventoryExtractionDocument("fixture/1", rows, hasSchema ? schema : null);
        var replay = InventoryRetainedSchemaProjection.Replay(SourceHash, "{}", retained, "new-normalizer/1");
        Assert.Equal(hasSchema ? 2 : 0, replay.Rows.Count);
        Assert.Equal(!hasSchema, replay.Document.SchemaDiscoveryFailure is not null);
        if (hasSchema)
            Assert.Equal(InventoryExtractionContract.Serialize(retained), replay.CanonicalJson);
    }

    [Fact]
    public async Task RejectedDiscoveryRetainsProviderEvidenceWithoutLegacyCommercialGuesses()
    {
        const string provider = """
            {"tables":[{"data":{"table_cells":[{"start_row_offset_idx":0,"start_col_offset_idx":0,"text":"Unseen heading"},{"start_row_offset_idx":1,"start_col_offset_idx":0,"text":"Raw observation"}]}}]}
            """;
        var (_, unrelatedSchema) = Fixture(2);
        var interpreter = new FixtureInterpreter(unrelatedSchema);
        var extraction = InventoryExtractionContract.Create("fixture", "1", "fixture/1", SourceHash, provider,
            [new(1, "legacy", new Dictionary<string, string> { ["supplier"] = "Unsupported guess" })]);
        var empty = new HashSet<string>();
        var codes = new InventoryCodeSets(empty, empty, empty, empty, empty, empty, empty, empty, empty);
        var step = new InventorySchemaExtractionStep(new InventorySchemaDiscoveryService(interpreter));
        var result = await step.ApplyAsync(extraction, codes, null, CancellationToken.None);
        Assert.Equal(1, interpreter.Calls);
        Assert.Equal(provider, result.ProviderJson);
        Assert.Empty(result.Rows);
        Assert.NotNull(result.Document.SchemaDiscoveryFailure);
        Assert.Null(result.Document.DiscoveredSchema);
    }

    private static (InventoryDocumentStructure Document, DiscoveredInventorySchema Schema) Fixture(int rows)
    {
        var cells = new List<InventorySourceCell>
        {
            new("row:0;column:0", 0, 0, "Nebular token"),
            new("row:0;column:1", 0, 1, "Consideration quantum"),
        };
        for (var row = 1; row <= rows; row++)
        {
            cells.Add(new($"row:{row};column:0", row, 0, $"unit-{row}"));
            cells.Add(new($"row:{row};column:1", row, 1, "  100.00  "));
        }
        var structures = new[] { new InventorySourceStructure("structure:1", "table", cells) };
        var document = new InventoryDocumentStructure(SourceHash, InventoryExtractionContract.Hash(JsonSerializer.Serialize(structures)), structures);
        InventorySchemaFieldMapping Mapping(string meaning, int column) => new(meaning,
            cells[column].RawText, cells[column].Locator, "structure:1", column, 0, false,
            "Deterministic test binding", 1m, [new(cells[column].Locator, cells[column].RawText)]);
        var schema = new DiscoveredInventorySchema(InventorySchemaValidation.ProtocolVersion, SourceHash, document.StructureHash,
            [new("structure:1", new(1, rows, 1, []), [Mapping("product_code", 0), Mapping("rate", 1)], [], [])],
            1m, [], new("fixture", "1", null, null, 0, 0));
        return (document, schema);
    }

    private sealed class FixtureInterpreter(DiscoveredInventorySchema schema) : IInventorySchemaInterpreter
    {
        internal int Calls { get; private set; }
        internal InventorySchemaDiscoveryRequest? Request { get; private set; }

        public Task<DiscoveredInventorySchema> DiscoverAsync(InventorySchemaDiscoveryRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            return Task.FromResult(schema);
        }
    }
}
