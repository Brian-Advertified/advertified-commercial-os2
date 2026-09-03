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
}
