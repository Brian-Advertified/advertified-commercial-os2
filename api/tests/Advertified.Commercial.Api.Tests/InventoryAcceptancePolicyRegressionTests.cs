using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryAcceptancePolicyRegressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('b', 64);
    private static readonly string[] Meanings =
        ["product_code", "name", "channel", "product_type", "geography", "rate", "currency", "rate_type", "description"];
    private static readonly string[] Values = ["unit-1", "Evidence product", MasterDataCodes.Channels.Digital,
        MasterDataCodes.InventoryProductTypes.DigitalPlacement, "Source region", "100.00",
        MasterDataCodes.Currencies.Zar, MasterDataCodes.RateTypes.FlatRate, ""];
    private static readonly InventoryCodeSets Codes = new(
        Set(MasterDataCodes.Channels.Digital), Set(MasterDataCodes.InventoryProductTypes.DigitalPlacement),
        Set(MasterDataCodes.RateTypes.FlatRate), Set(MasterDataCodes.Currencies.Zar),
        Set(MasterDataCodes.AvailabilityStatuses.PlanningAvailable), Set(), Set(), Set(), Set());

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    public void CompleteEvidenceAcceptsWithoutHumanSchemaApprovalAndAllowsOptionalAbsence(decimal confidence)
    {
        var extraction = Fixture(confidence);
        var candidate = Assert.Single(Evaluate(extraction));
        Assert.False(InventoryCandidateReviewPolicy.RequiresReview(candidate));
        var decision = InventoryAcceptancePolicy.Read(candidate.Values)!;
        Assert.Equal(InventoryAcceptancePolicy.Version, decision.PolicyVersion);
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Approved, decision.Outcome);
        Assert.Contains(decision.Checks, check => check.Check == InventoryAcceptanceCheck.CoordinateApplicability &&
            check.Result == InventoryAcceptanceCheckResult.NotApplicable && check.ApplicabilityCondition is not null);
        Assert.Equal(extraction.CanonicalOutputHash, decision.ExtractionRevision);
    }

    [Theory]
    [InlineData(InventoryAcceptanceCheckResult.Failed)]
    [InlineData(InventoryAcceptanceCheckResult.NotEvaluated)]
    [InlineData(InventoryAcceptanceCheckResult.NotApplicable)]
    public void MissingOrNonpositiveRequiredEvidenceCannotBeOverriddenByConfidence(InventoryAcceptanceCheckResult result)
    {
        var candidate = Assert.Single(Evaluate(Fixture(1)));
        var evaluation = InventoryAcceptancePolicy.Read(candidate.Values)!;
        var checks = evaluation.Checks.Select(check => check.Check == InventoryAcceptanceCheck.SourceIdentity
            ? check with { Result = result } : check).ToArray();
        Assert.False(InventoryAcceptancePolicy.Complete(checks));
        Assert.False(InventoryAcceptancePolicy.Complete([]));
        Assert.False(InventoryAcceptancePolicy.Complete(evaluation.Checks.Skip(1).ToArray()));
        Assert.NotEqual(MasterDataCodes.LifecycleStatuses.Approved, InventoryAcceptancePolicy.Outcome(checks));
    }

    [Theory]
    [InlineData("{\"tables\":[{}]}")]
    [InlineData("{\"pictures\":[{\"text\":\"\"}]}")]
    [InlineData("{\"pages\":{\"1\":{}}}")]
    public void UnsupportedOrMissingExtractedContentIsNotSilentlyAccounted(string provider)
    {
        var document = InventoryDocumentStructureReader.Read(Hash, provider);
        Assert.NotEmpty(document.ExtractionGaps!);
    }

    [Fact]
    public void UnreferencedFootnoteHoldsTheDocumentAndRetainsTheOriginalEvidence()
    {
        var extraction = Fixture(1, footnote: true);
        var candidate = Assert.Single(Evaluate(extraction));
        Assert.True(InventoryCandidateReviewPolicy.RequiresReview(candidate));
        Assert.Contains(InventoryAcceptancePolicy.Read(candidate.Values)!.Checks, check =>
            check.Check == InventoryAcceptanceCheck.SourceContentAccounting &&
            check.Result == InventoryAcceptanceCheckResult.Failed);
        Assert.Contains("Commercial condition", extraction.ProviderJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalOverridesAndSourceRevisionMismatchCannotBorrowAPassingDecision()
    {
        var extraction = Fixture(1);
        var candidate = Assert.Single(Evaluate(extraction));
        Assert.False(InventoryAcceptancePolicy.CanAccept(candidate.Values with { Name = "Invented replacement" }));
        Assert.Throws<InventoryPublishBlockedException>(() => InventoryRetainedAcceptance.EnsureMatches(
            new InventoryCandidateRow { RowNumber = 1, SourceLocator = candidate.SourceLocator,
                ValuesJson = JsonSerializer.Serialize(candidate.Values with { RateAmountMinor = 1 }, InventoryRowMapper.StoredJson) },
            [candidate]));
        var wrongSource = InventoryAcceptancePolicy.Apply(extraction, new string('c', 64), 1,
            Codes, [candidate], Now);
        Assert.True(InventoryCandidateReviewPolicy.RequiresReview(Assert.Single(wrongSource)));
    }

    [Fact]
    public void HumanMappingCorrectionRecordsRevisionAndRevalidatesWithoutChangingRawValues()
    {
        var extraction = Fixture(1);
        var original = extraction.Document.DiscoveredSchema!;
        var actor = Guid.NewGuid();
        var command = new ReviewInventoryCandidateCommand(MasterDataCodes.InventoryReviewDecisions.Edit,
            null, "Checked the commercial header binding.", null, original,
            InventoryAcceptancePolicy.MappingRevision(original));
        var corrected = InventoryInterpretationRevision.Correct(extraction, command, actor, Now, Codes);
        Assert.Equal(actor, corrected.Document.DiscoveredSchema!.Correction!.ActorId);
        Assert.Equal(command.ExpectedMappingRevision, corrected.Document.DiscoveredSchema.Correction.PreviousMappingRevision);
        Assert.NotEqual(command.ExpectedMappingRevision,
            InventoryAcceptancePolicy.MappingRevision(corrected.Document.DiscoveredSchema));
        Assert.Equal(extraction.ProviderJson, corrected.ProviderJson);
        Assert.Equal(original.Provenance, corrected.Document.DiscoveredSchema.Provenance);
        Assert.False(InventoryCandidateReviewPolicy.RequiresReview(Assert.Single(Evaluate(corrected))));
        Assert.Throws<VersionConflictException>(() => InventoryInterpretationRevision.Correct(extraction,
            command with { ExpectedMappingRevision = "stale" }, actor, Now, Codes));
        Assert.Throws<ArgumentException>(() => InventoryInterpretationRevision.Correct(extraction,
            command with { CorrectedValues = Assert.Single(Evaluate(extraction)).Values }, actor, Now, Codes));
    }

    internal static PreparedInventoryCandidate[] Evaluate(InventoryExtractionResult extraction)
    {
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(extraction.Rows, Hash, "", Codes, Now);
        return InventoryAcceptancePolicy.Apply(extraction, Hash, 1, Codes, candidates, Now);
    }

    internal static InventoryExtractionResult Fixture(decimal confidence, bool footnote = false,
        string? sourceHash = null, string? productCode = null, string? name = null)
    {
        var values = Values.ToArray();
        values[0] = productCode ?? values[0];
        values[1] = name ?? values[1];
        var cells = new List<object>();
        for (var column = 0; column < Meanings.Length; column++)
        {
            cells.Add(new { start_row_offset_idx = 0, start_col_offset_idx = column, text = $"Unfamiliar heading {column}" });
            if (column < Meanings.Length - 1)
                cells.Add(new { start_row_offset_idx = 1, start_col_offset_idx = column, text = values[column] });
        }
        if (footnote) cells.Add(new { start_row_offset_idx = 2, start_col_offset_idx = 0, text = "Commercial condition" });
        var provider = JsonSerializer.Serialize(new { tables = new[] { new { data = new { table_cells = cells } } } });
        var hash = sourceHash ?? Hash;
        var document = InventoryDocumentStructureReader.Read(hash, provider);
        var structure = document.Structures[0];
        var mappings = Meanings.Select((meaning, column) => new InventorySchemaFieldMapping(meaning,
            $"Unfamiliar heading {column}", $"{structure.Id};row=0;column={column}", structure.Id,
            column, 0, false, "Evidence-backed fixture binding", confidence,
            [new($"{structure.Id};row=0;column={column}", $"Unfamiliar heading {column}")])).ToArray();
        var schema = new DiscoveredInventorySchema(InventorySchemaValidation.ProtocolVersion, hash,
            document.StructureHash, [new(structure.Id, new(1, 1, 1, []), mappings, [], [])],
            confidence, [], new("deterministic-fixture", "fixture-prompt/1", null, null, 0, 0));
        var rows = InventorySchemaBatchProjection.Project(document, schema,
            InventoryCandidateNormalizer.CanonicalMeanings, InventorySchemaExtractionStep.GovernedCodes(Codes));
        return InventoryExtractionContract.Create("fixture", "1", "fixture/1", hash, provider, rows, schema);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedSchemaProviderRetainsRawSourceWithoutAcceptingRows(bool timeout)
    {
        var fixture = Fixture(1);
        var raw = InventoryExtractionContract.Create(fixture.AdapterCode, fixture.AdapterVersion,
            fixture.SchemaVersion, fixture.SourceHash, fixture.ProviderJson, fixture.Rows);
        var step = new InventorySchemaExtractionStep(new InventorySchemaDiscoveryService(
            new InterruptedInterpreter(timeout)));
        var result = await step.ApplyAsync(raw, Codes, null, CancellationToken.None);
        Assert.Equal(raw.SourceHash, result.SourceHash);
        Assert.Equal(raw.ProviderJson, result.ProviderJson);
        Assert.Null(result.Document.DiscoveredSchema);
        Assert.NotNull(result.Document.SchemaDiscoveryFailure);
        Assert.Empty(result.Rows);
    }

    private sealed class InterruptedInterpreter(bool timeout) : IInventorySchemaInterpreter
    {
        public Task<DiscoveredInventorySchema> DiscoverAsync(
            InventorySchemaDiscoveryRequest request, CancellationToken cancellationToken) =>
            Task.FromException<DiscoveredInventorySchema>(timeout
                ? new TaskCanceledException("Synthetic provider timeout")
                : new HttpRequestException("Synthetic provider interruption"));
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}
