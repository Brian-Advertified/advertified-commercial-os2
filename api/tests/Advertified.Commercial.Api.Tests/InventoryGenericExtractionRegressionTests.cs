using System.Diagnostics;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;
using Xunit.Abstractions;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryGenericExtractionRegressionTests(
    ITestOutputHelper output)
{
    private static readonly string SourceHash = new('b', 64);
    private static readonly string[] ExpectedGeographies = ["Coastal", "Inland"];
    private static readonly string[] ExpectedProductNames = ["Alpha", "Beta"];
    private static readonly int?[] ExpectedDurations = [15, 30];

    [Fact]
    public void HierarchicalMatrixRetainsProductsAndEveryRateCell()
    {
        var projected = ProjectSmallMatrix();

        Assert.Equal(2, projected.Length);
        Assert.Equal(8, projected.Sum(row => row.RateVariants!.Count));
        Assert.Equal("Alpha", projected[1].Values["name"]);
        Assert.Equal("docling:page=1;table=1;row=3;cell=1",
            projected[1].FieldLocators!["name"]);
        Assert.Equal("DERIVED_FROM_SOURCE_CONTEXT",
            projected[1].FieldTransformations!["name"]);
        Assert.All(projected.SelectMany(row => row.RateVariants!), rate =>
        {
            Assert.StartsWith("docling:page=1;table=1;", rate.SourceLocator);
            Assert.Equal(2, rate.HeaderLocators.Count);
            Assert.Contains("Rates", rate.HeaderHierarchy, StringComparison.Ordinal);
            Assert.NotNull(rate.DurationSeconds);
        });
    }

    [Fact]
    public void ReorderedGeographyMatrixPreservesSemanticRateVariants()
    {
        var rows = new[]
        {
            TableRow(0, "Product", "Geography", "Geography"),
            TableRow(1, "Name", "Coastal", "Inland"),
            TableRow(2, "Alpha", "R 200", "R 100"),
        };

        var projected = Assert.Single(
            InventoryHierarchicalMatrixProjection.Project(
                rows, 1, 0, RowLocator, CellLocator));
        Assert.Equal(ExpectedGeographies, projected.RateVariants!
            .Select(rate => rate.Geography).Order(StringComparer.Ordinal));
        var candidate = InventoryCandidateNormalizer.Normalize(
            projected, SourceHash, DateTimeOffset.UnixEpoch);
        Assert.Equal(2, candidate.Values.ProductVariants!.Count);
        Assert.Equal(2, candidate.Values.RateVariants!.Count);
    }

    [Fact]
    public void TransposedDurationMatrixCreatesProductsWithDistinctRates()
    {
        var rows = new[]
        {
            TableRow(0, "Duration", "Alpha", "Beta"),
            TableRow(1, "15 seconds", "R 100", "R 200"),
            TableRow(2, "30 seconds", "R 150", "R 250"),
        };

        var projected = InventoryHierarchicalMatrixProjection.Project(
            rows, 0, 0, RowLocator, CellLocator);

        Assert.Equal(ExpectedProductNames, projected.Select(row => row.Values["name"]));
        Assert.All(projected, row => Assert.Equal(2, row.RateVariants!.Count));
        Assert.Equal(ExpectedDurations, projected[0].RateVariants!
            .Select(rate => rate.DurationSeconds));
        Assert.Equal("docling:page=1;table=1;row=2;cell=2",
            projected[0].RateVariants![0].SourceLocator);
    }

    [Fact]
    public void DiscoveredSectionSchemaStillMaterialisesDenseMatrixRates()
    {
        var provider = ProviderJson(SmallMatrixRows());
        var document = InventoryDocumentStructureReader.Read(SourceHash, provider);
        var structure = Assert.Single(document.Structures);
        var nameHeader = structure.Cells.Single(cell =>
            cell.Row == 1 && cell.Column == 0);
        var mapping = new InventorySchemaFieldMapping("name", nameHeader.RawText,
            nameHeader.Locator, structure.Id, 0, 0, false,
            "Fixture section binding", 1m,
            [new(nameHeader.Locator, nameHeader.RawText)]);
        var schema = new DiscoveredInventorySchema(
            InventorySchemaValidation.ProtocolVersion, SourceHash,
            document.StructureHash,
            [new(structure.Id, new(2, 3, 1, []), [mapping], [], [])],
            1m, [], new("fixture", "1", null, null, 0, 0));

        var projected = InventorySchemaBatchProjection.Project(document, schema,
            new HashSet<string> { "name" },
            new Dictionary<string, IReadOnlySet<string>>());

        Assert.Equal(2, projected.Length);
        Assert.All(projected, row => Assert.NotNull(row.DiscoveredFields));
        Assert.Equal(8, projected.Sum(row => row.RateVariants!.Count));
    }

    [Fact]
    public void CommercialSourceAccountingHasNoSilentMatrixOmissions()
    {
        var rows = ProjectSmallMatrix();
        var extraction = InventoryExtractionContract.Create("fixture", "1", "fixture/1",
            SourceHash, ProviderJson(SmallMatrixRows()), rows);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(rows, SourceHash,
            string.Empty, EmptyCodes(), DateTimeOffset.UnixEpoch);

        var report = InventoryExtractionSourceAccounting.Build(
            extraction, candidates, []);

        Assert.Equal(0, report.Summary.UnaccountedCommercialElements);
        var product = Assert.Single(report.Entries, entry =>
            entry.SourceLocator == "docling:page=1;table=1;row=3;cell=1");
        Assert.Equal(InventoryExtractionTraceCodes.ProductField,
            product.TerminalDisposition);
        var rates = report.Entries.Where(entry =>
            entry.TerminalDisposition == InventoryExtractionTraceCodes.Rate).ToArray();
        Assert.Equal(8, rates.Length);
        Assert.All(rates, entry =>
        {
            Assert.Equal(InventoryExtractionTraceCodes.Mapped,
                entry.RecordProjection.State);
            Assert.NotNull(entry.HeaderHierarchy);
        });
        output.WriteLine(JsonSerializer.Serialize(report.Summary));
    }

    [Fact]
    public void RenamingAFileCannotChangeCandidateSemantics()
    {
        var rows = ProjectSmallMatrix();
        var first = InventoryCandidateAdmissionPolicy.Prepare(rows, SourceHash,
            string.Empty, EmptyCodes(), DateTimeOffset.UnixEpoch, "one.pdf");
        var second = InventoryCandidateAdmissionPolicy.Prepare(rows, SourceHash,
            string.Empty, EmptyCodes(), DateTimeOffset.UnixEpoch, "renamed.xlsx");

        Assert.Equal(
            JsonSerializer.Serialize(first.Select(item => item.Values)),
            JsonSerializer.Serialize(second.Select(item => item.Values)));
    }

    [Fact]
    public async Task SchemaDiscoveryRunsOncePerSectionAndNeverPerRecord()
    {
        var structures = new[] { Section("section:a"), Section("section:b") };
        var document = new InventoryDocumentStructure(SourceHash,
            InventoryExtractionContract.Hash(JsonSerializer.Serialize(structures)),
            structures);
        var interpreter = new SectionInterpreter();

        var result = await new InventorySchemaDiscoveryService(interpreter)
            .DiscoverAsync(document, new HashSet<string> { "product_code" },
                new Dictionary<string, IReadOnlySet<string>>(),
                CancellationToken.None);

        Assert.Equal(2, interpreter.Calls);
        Assert.Equal(2, result.Records.Count);
        Assert.All(interpreter.StructureCounts, count => Assert.Equal(1, count));
    }

    [Fact]
    public void FiftyRelatedExceptionsCreateOneReviewGroup()
    {
        var candidates = Enumerable.Range(1, 50).Select(number =>
            new PreparedInventoryCandidate(Guid.NewGuid(), number,
                new InventoryCandidateValues(null, "Item " + number, null, null,
                    null, null, null, null, null, null, null, null,
                    null, null),
                [new("rateVariant.currency", "INVALID", "Missing currency", true)],
                "docling:page=1;table=4;row=" + number,
                [], Guid.NewGuid())).ToArray();

        var group = Assert.Single(InventoryReviewGrouping.Group(candidates));
        Assert.Equal(50, group.AffectedCandidateCount);
        Assert.Equal("REPEATED_AMBIGUOUS_COLUMN", group.Cause);
        Assert.Contains("reproject", group.ActionSchemaJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailedSectionChecksDoNotContaminateACleanSection()
    {
        var row = ProjectSmallMatrix()[0] with
        {
            DiscoveredFields =
            [
                new("name", "Name", "Alpha", "section:clean;row=2;cell=1",
                    "section:clean", null, "fixture", 1m, null, []),
            ],
        };
        var checks = new InventoryAcceptanceCheckEvidence[]
        {
            new(InventoryAcceptanceCheck.SourceIdentity,
                InventoryAcceptanceCheckResult.Passed, "document", "bound"),
            new(InventoryAcceptanceCheck.StructuralApplication,
                InventoryAcceptanceCheckResult.Passed, "section:clean", "replayed"),
            new(InventoryAcceptanceCheck.SourceContentAccounting,
                InventoryAcceptanceCheckResult.Passed, "section:clean", "accounted"),
            new(InventoryAcceptanceCheck.StructuralApplication,
                InventoryAcceptanceCheckResult.Failed, "section:failed", "mismatch"),
            new(InventoryAcceptanceCheck.SourceContentAccounting,
                InventoryAcceptanceCheckResult.Failed, "section:failed", "missing"),
        };

        var applicable = InventoryAcceptancePolicy.ApplicableDocumentChecks(checks, row);

        Assert.Equal(3, applicable.Length);
        Assert.All(applicable, check =>
            Assert.Equal(InventoryAcceptanceCheckResult.Passed, check.Result));
    }

    [Fact]
    public void FiveThousandRateVariantsProjectWithinBoundedResources()
    {
        var rows = LargeMatrixRows(1_000, 5);
        _ = InventoryHierarchicalMatrixProjection.Project(rows, 1, 0,
            RowLocator, CellLocator);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();

        var projected = InventoryHierarchicalMatrixProjection.Project(
            rows, 1, 0, RowLocator, CellLocator);

        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1_000, projected.Length);
        Assert.Equal(5_000, projected.Sum(row => row.RateVariants!.Count));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
        Assert.True(allocated < 256L * 1024 * 1024);
        var replay = InventoryHierarchicalMatrixProjection.Project(
            rows, 1, 0, RowLocator, CellLocator);
        Assert.Equal(JsonSerializer.Serialize(projected),
            JsonSerializer.Serialize(replay));
        output.WriteLine("5,000 variants: {0:F1} ms; {1:F2} MiB allocated; {2} product rows.",
            timer.Elapsed.TotalMilliseconds, allocated / 1024d / 1024d,
            projected.Length);
    }

    private static InventoryExtractedRow[] ProjectSmallMatrix() =>
        InventoryHierarchicalMatrixProjection.Project(
            SmallMatrixRows(), 1, 0, RowLocator, CellLocator,
            position: (row, column) => JsonSerializer.Serialize(new { row, column }));

    private static InventoryTableRow[] SmallMatrixRows() =>
    [
        TableRow(0, "Product", "Rates", "Rates", "Rates", "Rates"),
        TableRow(1, "Name", "15 seconds", "30 seconds", "45 seconds", "60 seconds"),
        TableRow(2, "Alpha", "R 100", "R 200", "R 300", "R 400"),
        TableRow(3, "", "R 110", "R 210", "R 310", "R 410"),
    ];

    private static InventoryTableRow[] LargeMatrixRows(int products, int rates)
    {
        var rows = new List<InventoryTableRow>
        {
            TableRow(0, Enumerable.Range(0, rates + 1)
                .Select(index => index == 0 ? "Product" : "Rates").ToArray()),
            TableRow(1, Enumerable.Range(0, rates + 1)
                .Select(index => index == 0 ? "Name" : index * 15 + " seconds").ToArray()),
        };
        for (var product = 1; product <= products; product++)
            rows.Add(TableRow(product + 1, Enumerable.Range(0, rates + 1)
                .Select(index => index == 0 ? "Product " + product :
                    "R " + (product * 100 + index)).ToArray()));
        return rows.ToArray();
    }

    private static InventoryTableRow TableRow(int row, params string[] values) =>
        new(row, values.Select((value, column) => (value, column))
            .ToDictionary(item => item.column, item => item.value),
            values.Select((_, column) => column)
                .ToDictionary(column => column, column => CellLocator(row, column)));

    private static string RowLocator(int row) => CellLocator(row, 0);
    private static string CellLocator(int row, int column) =>
        $"docling:page=1;table=1;row={row + 1};cell={column + 1}";

    private static string ProviderJson(IEnumerable<InventoryTableRow> rows) =>
        JsonSerializer.Serialize(new
        {
            tables = new[]
            {
                new
                {
                    data = new
                    {
                        table_cells = rows.SelectMany(row => row.Cells.Select(cell => new
                        {
                            start_row_offset_idx = row.SourceRow,
                            start_col_offset_idx = cell.Key,
                            text = cell.Value,
                        })).ToArray(),
                    },
                },
            },
        });

    private static InventorySourceStructure Section(string id) => new(id, "table",
    [
        new(id + ";row=1;cell=1", 0, 0, "Product"),
        new(id + ";row=2;cell=1", 1, 0, id + "-001"),
    ]);

    private static InventoryCodeSets EmptyCodes()
    {
        var empty = new HashSet<string>();
        return new(empty, empty, empty, empty, empty, empty, empty, empty, empty);
    }

    private sealed class SectionInterpreter : IInventorySchemaInterpreter
    {
        internal int Calls { get; private set; }
        internal List<int> StructureCounts { get; } = [];

        public Task<DiscoveredInventorySchema> DiscoverAsync(
            InventorySchemaDiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            StructureCounts.Add(request.RepresentativeStructures.Count);
            var structure = Assert.Single(request.RepresentativeStructures);
            var header = structure.Cells[0];
            var mapping = new InventorySchemaFieldMapping("product_code",
                header.RawText, header.Locator, structure.Id, 0, 0, false,
                "Fixture section binding", 1m,
                [new(header.Locator, header.RawText)]);
            return Task.FromResult(new DiscoveredInventorySchema(
                InventorySchemaValidation.ProtocolVersion, request.SourceHash,
                request.StructureHash,
                [new(structure.Id, new(1, 1, 1, []), [mapping], [], [])],
                1m, [], new("fixture", "1", null, null, 0, 0)));
        }
    }
}
