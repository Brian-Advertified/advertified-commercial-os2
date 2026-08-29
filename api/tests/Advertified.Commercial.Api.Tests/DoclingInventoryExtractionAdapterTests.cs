using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public async Task MapsPinnedStructuredTableAndCoordinatesWithoutVendorTypes()
    {
        var response = JsonSerializer.Serialize(new
        {
            status = "success",
            document = new
            {
                json_content = new
                {
                    tables = new[]
                    {
                        new
                        {
                            prov = new[] { new { page_no = 2 } },
                            data = new
                            {
                                table_cells = new object[]
                                {
                                    Cell("Product Code", 0, 0), Cell("Rate", 0, 1),
                                    Cell("SITE-1", 1, 0), Cell("1250.00", 1, 1),
                                },
                            },
                        },
                    },
                },
                text_content = string.Empty,
            },
        });
        using var client = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal("local-contract-key", request.Headers.GetValues("X-Api-Key").Single());
            Assert.Equal("/v1/convert/file", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        })) { BaseAddress = new Uri("http://docling.test") };
        var settings = Options.Create(new InventoryExtractionOptions
        {
            Mode = InventoryExtractionOptions.DoclingMode,
            BaseUrl = "http://docling.test",
            ApiKey = "local-contract-key",
        });
        var adapter = new DoclingInventoryExtractionAdapter(client, settings);

        var result = await adapter.ExtractAsync(new InventoryExtractionRequest(
            "rates.pdf", "application/pdf", "PDF", new string('a', 64), [1, 2, 3]),
            CancellationToken.None);

        Assert.Equal("docling", result.AdapterCode);
        Assert.Equal(InventoryExtractionOptions.PinnedAdapterVersion, result.AdapterVersion);
        Assert.Equal("SITE-1", Assert.Single(result.Rows).Values["productcode"]);
        Assert.Equal("docling:page=2;table=1;row=2", result.Rows[0].Locator);
        Assert.Equal(64, result.OutputHash.Length);
    }

    private static object Cell(string text, int row, int column) => new
    {
        text,
        start_row_offset_idx = row,
        start_col_offset_idx = column,
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
