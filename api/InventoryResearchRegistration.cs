using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api;

internal static class InventoryResearchRegistration
{
    internal static void AddInventoryResearch(this IServiceCollection services)
    {
        services.AddScoped<IInventoryResearchCommands, InventoryResearchCommands>();
        services.AddScoped<IInventoryResearchReader, InventoryResearchReader>();
    }
}
