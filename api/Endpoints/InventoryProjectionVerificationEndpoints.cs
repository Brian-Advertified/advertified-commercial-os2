using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api.Endpoints;

internal static class InventoryProjectionVerificationEndpoints
{
    internal static IEndpointRouteBuilder
        MapInventoryProjectionVerificationEndpoints(
            this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/internal/tenants/{tenantId:guid}/inventory-projection-verification",
                VerifyAsync)
            .WithTags("Local verification")
            .RequireAuthorization()
            .RequireRateLimiting(RequestRateLimitPolicies.InventoryUpload)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<InventoryProjectionVerificationView>()
            .WithQueryProblems();
        return endpoints;
    }

    private static async Task<InventoryProjectionVerificationView> VerifyAsync(
        Guid tenantId,
        HttpContext context,
        ICurrentIdentity identity,
        InventoryProjectionVerificationService verifier,
        CancellationToken cancellationToken)
    {
        var (_, source) = await InventoryUploadBody.ReadAsync(
            context, cancellationToken);
        return await verifier.VerifyAsync(
            identity.ActorId,
            new TenantId(tenantId),
            source,
            cancellationToken);
    }
}
