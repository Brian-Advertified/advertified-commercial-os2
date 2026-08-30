using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class CreativeEndpoints
{
    public static IEndpointRouteBuilder MapCreativeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var campaigns = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/campaigns")
            .WithTags("Creative delivery").RequireAuthorization();
        campaigns.MapPost("/{campaignId:guid}:request-creative", RequestAsync)
            .WithName("RequestCampaignCreative").Produces<CampaignView>()
            .WithCommandProblems(requiresVersion: true);
        campaigns.MapPost("/{campaignId:guid}/creative", CreateAssetAsync)
            .WithName("CreateCreativeAsset").Produces<CreativeAssetView>()
            .WithCommandProblems(requiresVersion: false);
        campaigns.MapPost(
                "/{campaignId:guid}/creative/{assetId:guid}:upload-version", UploadVersionAsync)
            .WithName("UploadCreativeAssetVersion").Produces<CreativeAssetView>()
            .WithCommandProblems(requiresVersion: true);
        campaigns.MapPost(
                "/{campaignId:guid}/creative/{assetId:guid}:brand-review", ReviewBrandAsync)
            .WithName("ReviewCreativeBrand").Produces<CreativeAssetView>()
            .WithCommandProblems(requiresVersion: true);
        campaigns.MapPost("/{campaignId:guid}:approve-creative", ApproveCampaignAsync)
            .WithName("ApproveCampaignCreative").Produces<CampaignView>()
            .WithCommandProblems(requiresVersion: true);

        var assets = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/creative-assets")
            .WithTags("Creative delivery").RequireAuthorization();
        assets.MapGet("/{assetId:guid}", GetSupplierAssetAsync)
            .WithName("GetSupplierCreativeAsset").Produces<SupplierCreativeAssetView>()
            .WithQueryProblems();
        assets.MapPost("/{assetId:guid}:supplier-review", ReviewSupplierAsync)
            .WithName("ReviewCreativeSupplier").Produces<SupplierCreativeAssetView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static Task<IResult> RequestAsync(
        Guid tenantId, Guid campaignId, RequestCampaignCreativeCommand command,
        HttpContext context, ICurrentIdentity identity, ICreativeCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock,
            (envelope, token) => commands.RequestAsync(campaignId, envelope, token),
            cancellationToken);

    private static Task<IResult> CreateAssetAsync(
        Guid tenantId, Guid campaignId, CreateCreativeAssetCommand command,
        HttpContext context, ICurrentIdentity identity, ICreativeCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, false,
            (envelope, token) => commands.CreateAssetAsync(campaignId, envelope, token),
            cancellationToken);

    private static Task<IResult> UploadVersionAsync(
        Guid tenantId, Guid campaignId, Guid assetId,
        UploadCreativeAssetVersionCommand command, HttpContext context,
        ICurrentIdentity identity, ICreativeCommands commands, TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock,
            (envelope, token) => commands.UploadVersionAsync(
                campaignId, assetId, envelope, token), cancellationToken);

    private static Task<IResult> ReviewBrandAsync(
        Guid tenantId, Guid campaignId, Guid assetId,
        ReviewCreativeBrandCommand command, HttpContext context,
        ICurrentIdentity identity, ICreativeCommands commands, TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock,
            (envelope, token) => commands.ReviewBrandAsync(
                campaignId, assetId, envelope, token), cancellationToken);

    private static Task<IResult> ApproveCampaignAsync(
        Guid tenantId, Guid campaignId, ApproveCampaignCreativeCommand command,
        HttpContext context, ICurrentIdentity identity, ICreativeCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock,
            (envelope, token) => commands.ApproveCampaignAsync(campaignId, envelope, token),
            cancellationToken);

    private static async Task<IResult> GetSupplierAssetAsync(
        Guid tenantId, Guid assetId, HttpContext context, ICurrentIdentity identity,
        ICreativeReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.GetSupplierAssetAsync(
            identity.ActorId, new TenantId(tenantId), assetId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<IResult> ReviewSupplierAsync(
        Guid tenantId, Guid assetId, ReviewCreativeSupplierCommand command,
        HttpContext context, ICurrentIdentity identity, ICreativeCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock,
            (envelope, token) => commands.ReviewSupplierAsync(assetId, envelope, token),
            cancellationToken);

    private static Task<IResult> ExecuteAsync<TCommand, TView>(
        Guid tenantId, TCommand command, HttpContext context, ICurrentIdentity identity,
        TimeProvider clock,
        Func<CommandEnvelope<TCommand>, CancellationToken,
            Task<CommandResult<TView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TView : notnull => CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true, execute, cancellationToken);
}
