using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryInterpretationRevisionTests
{
    [Fact]
    public void StructureIdentitySurvivesJsonObjectReorderingButNotChangedRawValues()
    {
        const string first = """{"texts":[{"text":"Raw source","prov":[{"page_no":1,"bbox":{"l":1,"t":2}}]}]}""";
        const string reordered = """{"texts":[{"prov":[{"bbox":{"t":2,"l":1},"page_no":1}],"text":"Raw source"}]}""";
        var original = InventoryDocumentStructureReader.Read("fixture", first);
        Assert.Equal(original.StructureHash, InventoryDocumentStructureReader.Read("fixture", reordered).StructureHash);
        Assert.NotEqual(original.StructureHash, InventoryDocumentStructureReader.Read("fixture",
            reordered.Replace("Raw source", "Changed source", StringComparison.Ordinal)).StructureHash);
    }

    [Fact]
    public void RejectedSourceBindingsSurviveRenumberingAndSplittingWithoutHoldingUnrelatedRecords()
    {
        var original = InventoryAcceptancePolicyRegressionTests.Fixture(1);
        var row = original.Rows[0];
        var moved = row with { Number = 7, Locator = "revised-boundary" };
        var unrelated = row with { Number = 8, Locator = "other-boundary", DiscoveredFields = [] };
        var corrected = InventoryExtractionContract.Create(original.AdapterCode, original.AdapterVersion,
            original.SchemaVersion, original.SourceHash, original.ProviderJson, [moved, unrelated], original.Document.DiscoveredSchema);
        var candidate = InventoryAcceptancePolicyRegressionTests.Evaluate(original)[0];
        var split = candidate with { Id = Guid.NewGuid(), RowNumber = 7 };
        var other = candidate with { Id = Guid.NewGuid(), RowNumber = 8 };
        var rejected = InventoryRejectionCarryForward.Match(original, corrected,
            [new() { RowNumber = row.Number, SourceLocator = row.Locator, Status = MasterDataCodes.LifecycleStatuses.Rejected }],
            [split, other]);
        Assert.Contains(split.Id, rejected);
        Assert.DoesNotContain(other.Id, rejected);
    }

    [Fact]
    public void MissingRejectedSourceBindingFailsClosed()
    {
        var original = InventoryAcceptancePolicyRegressionTests.Fixture(1);
        Assert.Throws<InventoryPublishBlockedException>(() => InventoryRejectionCarryForward.Match(original, original,
            [new() { RowNumber = 999, SourceLocator = "missing", Status = MasterDataCodes.LifecycleStatuses.Rejected }], []));
    }
}
