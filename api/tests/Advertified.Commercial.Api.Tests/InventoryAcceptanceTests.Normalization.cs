using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private static async Task AssertCanonicalAliasCollisionAsync(HttpClient importer)
    {
        using var upload = await UploadAsync(
            importer, "inventory-alias-collision-upload", "Alias Supplier",
            CanonicalAliasCollisionFixture());
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-alias-collision-execute", 1, new { });
        using var extracted = await ReadJsonAsync(execute);
        var candidate = Assert.Single(
            extracted.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.Equal("Accepted Name",
            candidate.GetProperty("values").GetProperty("name").GetString());
        var nameEvidence = candidate.GetProperty("evidence").EnumerateArray()
            .Where(item => item.GetProperty("fieldName").GetString() == "name")
            .ToArray();
        Assert.Single(nameEvidence);
        Assert.Equal("Accepted Name", nameEvidence[0].GetProperty("rawValue").GetString());
    }

    private static async Task AssertContextualCommercialRowAsync(HttpClient importer)
    {
        using var upload = await UploadAsync(
            importer, "inventory-contextual-commercial-upload", "Algoa FM",
            ContextualCommercialFixture());
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-contextual-commercial-execute", 1, new { });
        using var extracted = await ReadJsonAsync(execute);
        var candidate = Assert.Single(
            extracted.RootElement.GetProperty("candidates").EnumerateArray());
        var values = candidate.GetProperty("values");
        Assert.Equal("Generic 30-second recorded commercial",
            values.GetProperty("name").GetString());
        Assert.Equal("ZAR", values.GetProperty("currency").GetString());
        Assert.Equal(29_106_000, values.GetProperty("rateAmountMinor").GetInt64());
        Assert.Equal("PLANNING_AVAILABLE", values.GetProperty("availability").GetString());
    }
}
