using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public async Task ScheduleMatrixProjectsDatedSpotsWithExactEvidence()
    {
        var response = JsonSerializer.Serialize(new
        {
            status = "success",
            document = new
            {
                json_content = new
                {
                    texts = Array.Empty<object>(),
                    tables = new[]
                    {
                        Table(
                            Cell("Time", 0, 0),
                            Cell("2026/09/04", 0, 1),
                            Cell("2026/09/05", 0, 2),
                            Cell("02:00", 1, 0),
                            Cell("The Full View\nR8 000", 1, 1),
                            Cell("News Desk\nR9 500", 1, 2)),
                    },
                },
            },
        });
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response, Encoding.UTF8, "application/json"),
            }))
        {
            BaseAddress = new Uri("http://docling.test"),
        };
        var adapter = new DoclingInventoryExtractionAdapter(
            client,
            Options.Create(new InventoryExtractionOptions
            {
                Mode = InventoryExtractionOptions.DoclingMode,
                BaseUrl = "http://docling.test",
                ApiKey = "local-contract-key",
            }));
        var hash = new string('a', 64);

        var result = await adapter.ReadResultAsync(
            new InventoryExtractionRequest(
                "SABC TV rates.pdf",
                "application/pdf",
                "PDF",
                hash,
                [1]),
            Guid.NewGuid().ToString(),
            CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        var row = result.Rows[0];
        Assert.Equal("The Full View", row.Values["name"]);
        Assert.Equal("R8 000", row.Values["rate"]);
        Assert.Equal("2026/09/04", row.Values["scheduledate"]);
        Assert.Equal("02:00", row.Values["timeslot"]);
        Assert.False(row.Values.ContainsKey("channel")); // A TV filename is not source evidence.
        Assert.Equal(
            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
            row.FieldEvidenceBases!["ratetype"]);
        Assert.Equal(
            MasterDataCodes.InventoryTransformationTypes
                .DerivedFromSourceContext,
            row.FieldTransformations!["ratetype"]);

        var candidate = InventoryCandidateNormalizer.Normalize(
            row, hash, DateTimeOffset.Parse(
                "2026-09-03T00:00:00Z",
                CultureInfo.InvariantCulture));
        Assert.Equal(800_000, candidate.Values.RateAmountMinor);
        Assert.Equal(MasterDataCodes.RateTypes.SpotRate,
            candidate.Values.RateType);
        Assert.Contains(candidate.Evidence, item =>
            item.FieldName == "rate_type" &&
            item.EvidenceBasis ==
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy);
    }
}
