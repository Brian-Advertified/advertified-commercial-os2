using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryPendingSupplierPolicyTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void PendingSupplierRemovesOnlyDeferredPricingBlockers()
    {
        var values = CandidateValues(pendingSupplier: true);
        var issues = new[]
        {
            Issue("rateType"),
            Issue("currency"),
            Issue("rateAmountMinor"),
            Issue("geography"),
        };

        var result = InventoryPendingSupplierValidationPolicy.Apply(
            values, issues);

        var issue = Assert.Single(result);
        Assert.Equal("geography", issue.FieldName);
        Assert.True(issue.IsBlocking);
    }

    [Fact]
    public void OrdinaryCandidateKeepsPricingBlockers()
    {
        var values = CandidateValues(pendingSupplier: false);
        var issues = new[] { Issue("rateAmountMinor") };

        var result = InventoryPendingSupplierValidationPolicy.Apply(
            values, issues);

        Assert.Single(result);
    }

    private static InventoryCandidateValues CandidateValues(
        bool pendingSupplier)
    {
        var extension = pendingSupplier
            ? "\"extension\":{\"pricingstatus\":\"PENDING_SUPPLIER\"},"
            : string.Empty;
        var json = "{" + extension +
            "\"productCode\":\"ADV-TEST\"," +
            "\"name\":\"Test placement\"," +
            "\"channel\":\"OOH\"," +
            "\"productType\":\"OOH_SITE\"," +
            "\"geography\":\"Johannesburg\"," +
            "\"availability\":\"AVAILABLE\"}";
        return JsonSerializer.Deserialize<InventoryCandidateValues>(
            json,
            SerializerOptions)
            ?? throw new InvalidOperationException();
    }

    private static InventoryValidationIssueView Issue(string field) =>
        new(field, "REQUIRED", "Required for this test.", true);
}
