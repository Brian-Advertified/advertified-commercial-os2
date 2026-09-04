using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void DigitalPlatformTableRepairsShiftedCpmAndContinuationRows()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = Array.Empty<object>(),
            tables = new[]
            {
                Table(
                    Cell("Platform", 0, 0),
                    Cell("Element", 0, 1),
                    Cell("Specifications", 0, 2),
                    Cell("CPM", 0, 3),
                    Cell("", 0, 4),
                    Cell("Display", 1, 0),
                    Cell("Leaderboard", 1, 1),
                    Cell("728 x 90 max 39k", 1, 2),
                    Cell("R365", 1, 3),
                    Cell("", 1, 4),
                    Cell("", 2, 0),
                    Cell("Medium Rectangle", 2, 1),
                    Cell("300 x 250 max 39k", 2, 2),
                    Cell("", 2, 3),
                    Cell("R365", 2, 4),
                    Cell("", 3, 0),
                    Cell("Half Page Advertisement", 3, 1),
                    Cell("300 x 600 max 39k", 3, 2),
                    Cell("", 3, 3),
                    Cell("R415", 3, 4)),
            },
        });
        var request = new InventoryExtractionRequest(
            "JAC Rate Card_2026.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('e', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            json,
            rows);
        var contextual = InventorySourceContextProjection.Apply(
            request,
            provider);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            contextual.Rows,
            request.SourceHash,
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(3, candidates.Length);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal("Display", candidate.Values.Name);
            Assert.Equal(
                MasterDataCodes.Channels.Digital,
                candidate.Values.Channel);
            Assert.Equal(
                MasterDataCodes.RateTypes.Cpm,
                candidate.Values.RateType);
        });
        Assert.Collection(
            candidates.OrderBy(item => item.RowNumber),
            item =>
            {
                Assert.Equal(
                    "Leaderboard",
                    item.Values.Deliverable!.Placement);
                Assert.Equal(36_500, item.Values.RateAmountMinor);
            },
            item =>
            {
                Assert.Equal(
                    "Medium Rectangle",
                    item.Values.Deliverable!.Placement);
                Assert.Equal(36_500, item.Values.RateAmountMinor);
            },
            item =>
            {
                Assert.Equal(
                    "Half Page Advertisement",
                    item.Values.Deliverable!.Placement);
                Assert.Equal(41_500, item.Values.RateAmountMinor);
            });
    }
}
