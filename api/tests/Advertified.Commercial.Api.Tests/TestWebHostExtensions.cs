using Microsoft.AspNetCore.Hosting;

namespace Advertified.Commercial.Api.Tests;

internal static class TestWebHostExtensions
{
    internal static IWebHostBuilder UseDeterministicInventoryProtection(
        this IWebHostBuilder builder)
    {
        builder.UseSetting("InventoryProtection:ObjectStoreMode", "InMemory");
        builder.UseSetting("InventoryProtection:ScannerMode", "Deterministic");
        return builder;
    }
}
