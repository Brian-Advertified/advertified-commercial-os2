using System.Net;
using System.Text;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private static async Task AssertProtectionBoundariesAsync(
        HttpClient importer,
        IInventoryObjectStore objectStore)
    {
        var oversizedBytes = new byte[InventoryProtectionOptions.MaximumSupportedSourceBytes + 1];
        Encoding.UTF8.GetBytes("product_code,name\n").CopyTo(oversizedBytes, 0);
        var oversized = new FileFixture(
            "CSV", "oversized.csv", "text/csv", oversizedBytes);
        using var oversizedResponse = await UploadAsync(
            importer, "inventory-size-boundary", "Boundary Supplier", oversized);
        await AssertProblemAsync(
            oversizedResponse, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var mismatch = new FileFixture(
            "PNG", "not-an-image.csv", "text/csv",
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        using var mismatchResponse = await UploadAsync(
            importer, "inventory-type-mismatch", "Boundary Supplier", mismatch);
        await AssertProblemAsync(
            mismatchResponse, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var unsafeSource = new FileFixture(
            "CSV", "unsafe.csv", "text/csv", Encoding.ASCII.GetBytes(
                "product_code,name\nEICAR-STANDARD-ANTIVIRUS-TEST-FILE,unsafe\n"));
        using var unsafeResponse = await UploadAsync(
            importer, "inventory-malware", "Boundary Supplier", unsafeSource);
        using var unsafeJson = await ReadJsonAsync(unsafeResponse);
        Assert.Equal("FAILED", unsafeJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("INFECTED", unsafeJson.RootElement.GetProperty("scanStatus").GetString());
        Assert.Equal("MALWARE_DETECTED", unsafeJson.RootElement.GetProperty("failureCode").GetString());
        var unsafeId = unsafeJson.RootElement.GetProperty("id").GetGuid();
        var unsafeHash = unsafeJson.RootElement.GetProperty("sourceHash").GetString();
        Assert.True(await objectStore.ExistsAsync(
            $"quarantine/{TenantId:N}/{unsafeId:N}/{unsafeHash}", default));
        Assert.False(await objectStore.ExistsAsync(
            $"protected/{TenantId:N}/{unsafeHash}", default));

        using var cleanResponse = await UploadAsync(
            importer, "inventory-single-copy", "Boundary Supplier", CsvFixture());
        using var cleanJson = await ReadJsonAsync(cleanResponse);
        var cleanId = cleanJson.RootElement.GetProperty("id").GetGuid();
        var cleanHash = cleanJson.RootElement.GetProperty("sourceHash").GetString();
        Assert.True(await objectStore.ExistsAsync(
            $"protected/{TenantId:N}/{cleanHash}", default));
        Assert.False(await objectStore.ExistsAsync(
            $"quarantine/{TenantId:N}/{cleanId:N}/{cleanHash}", default));
    }
}
