using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task AssertIneligibleRemovalIsStillPresentAsync(
        HttpClient client, string connectionString, Guid productId)
    {
        await AppendUnavailableInventoryEvidenceAsync(connectionString, productId);
        using var shortlist = await CommandAsync(client,
            Path($"brief-versions/{BriefVersionId}/shortlists:generate"), "decision-unavailable-shortlist", 1, new { });
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        var candidates = shortlist.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.False(candidates.Single(item => item.GetProperty("inventoryProductId").GetGuid() == productId)
            .GetProperty("isEligible").GetBoolean());
        var selectedId = candidates.First(item => item.GetProperty("isEligible").GetBoolean())
            .GetProperty("id").GetGuid();
        using var selected = await CommandAsync(client, Path($"shortlist-versions/{shortlistId}:select"),
            "decision-unavailable-select", 1, new
            {
                selectedCandidateIds = new[] { selectedId }, reason = "Original site is no longer available.",
            });
        using var response = await client.GetAsync(Path($"reporting/inventory-decisions?briefVersionId={BriefVersionId}"));
        response.EnsureSuccessStatusCode();
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var removed = report.RootElement.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("shortlistVersionId").GetGuid() == shortlistId &&
            item.GetProperty("productId").GetGuid() == productId);
        Assert.True(removed.GetProperty("presentInCurrentShortlist").GetBoolean());
        Assert.True(removed.GetProperty("wasSelected").GetBoolean());
        Assert.False(removed.GetProperty("isSelected").GetBoolean());
    }

    private static async Task AppendUnavailableInventoryEvidenceAsync(string connectionString, Guid productId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO commercial.inventory_availability
                (id, tenant_id, product_version_id, availability_code, observed_at_utc, valid_until_utc, source_locator)
            SELECT $1, tenant_id, current_version_id, 'UNAVAILABLE', $2, $3, 'synthetic:unavailable-regression'
            FROM commercial.inventory_products WHERE tenant_id = $4 AND id = $5
            """, connection);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(Now.AddMinutes(1));
        command.Parameters.AddWithValue(Now.AddMonths(3));
        command.Parameters.AddWithValue(TenantId);
        command.Parameters.AddWithValue(productId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
