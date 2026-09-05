using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Inventory truth").RequireAuthorization();
        group.MapPost("/inventory-imports", CreateImportAsync)
            .WithName("CreateInventoryImport")
            .RequireRateLimiting(RequestRateLimitPolicies.InventoryUpload)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<InventoryImportView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/inventory-imports/{importId:guid}:execute", ExecuteImportAsync)
            .WithName("ExecuteInventoryImport").Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:retry-extraction", RetryExtractionAsync)
            .WithName("RetryInventoryExtraction").Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:cancel-extraction", CancelExtractionAsync)
            .WithName("CancelInventoryExtraction").Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:reconcile-extraction",
                ReconcileExtractionAsync)
            .WithName("ReconcileInventoryExtraction").Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:reproject-extraction",
                ReprojectExtractionAsync)
            .WithName("ReprojectInventoryExtraction")
            .Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-imports/{importId:guid}", GetImportAsync)
            .WithName("GetInventoryImport").Produces<InventoryImportView>()
            .WithQueryProblems();
        group.MapGet("/inventory-imports/{importId:guid}/source", GetImportSourceAsync)
            .WithName("GetInventoryImportSource").Produces(StatusCodes.Status200OK)
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork).WithQueryProblems();
        group.MapGet(
                "/inventory-semantic-preflight",
                GetSemanticPreflightAsync)
            .WithName("GetInventorySemanticPreflight")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces<InventorySemanticPreflightView>()
            .WithQueryProblems();
        group.MapPost("/inventory-candidates/{candidateId:guid}:review", ReviewCandidateAsync)
            .WithName("ReviewInventoryCandidate").Produces<InventoryCandidateView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:publish", PublishImportAsync)
            .WithName("PublishInventoryImport").Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-products", SearchProductsAsync)
            .WithName("SearchInventoryProducts").Produces<InventoryProductPage>()
            .WithQueryProblems();
        group.MapGet("/inventory-products/{productId:guid}", GetProductAsync)
            .WithName("GetInventoryProduct").Produces<InventoryProductView>()
            .WithQueryProblems();
        group.MapPost("/inventory-assets/{assetId:guid}:review-rights", ReviewAssetRightsAsync)
            .WithName("ReviewInventoryAssetRights")
            .Produces<InventoryAssetRightsReviewView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-products/{productId:guid}/assets", UploadAssetAsync)
            .WithName("UploadInventoryAsset")
            .RequireRateLimiting(RequestRateLimitPolicies.InventoryUpload)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<InventoryAssetView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/inventory-products/{productId:guid}/availability-exceptions",
                RecordAvailabilityExceptionAsync)
            .WithName("RecordInventoryAvailabilityException")
            .Produces<InventoryAvailabilityExceptionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-assets/{assetId:guid}/content", GetApprovedAssetAsync)
            .WithName("GetApprovedInventoryAsset")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces(StatusCodes.Status200OK)
            .WithQueryProblems();
        group.MapPost("/inventory-products/{productId:guid}/embedding", SubmitEmbeddingAsync)
            .WithName("SubmitInventoryEmbedding").Produces<InventoryEmbeddingView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-products/{productId:guid}/semantic-recall", GetSemanticRecallAsync)
            .WithName("GetInventorySemanticRecall")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces<IReadOnlyList<InventorySemanticRecallView>>()
            .WithQueryProblems();
        group.MapPost("/inventory-products/{productId:guid}/semantic-duplicate-candidates",
                NominateSemanticDuplicateAsync)
            .WithName("NominateInventorySemanticDuplicate")
            .Produces<InventoryDuplicateCandidateView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-duplicate-candidates", ListDuplicateCandidatesAsync)
            .WithName("ListInventoryDuplicateCandidates")
            .Produces<IReadOnlyList<InventoryDuplicateCandidateView>>()
            .WithQueryProblems();
        group.MapPost("/inventory-duplicate-candidates/{candidateId:guid}:review",
                ReviewDuplicateAsync)
            .WithName("ReviewInventoryDuplicateCandidate")
            .Produces<InventoryDuplicateCandidateView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/inventory-products/{productId:guid}/benchmark", GetBenchmarkAsync)
            .WithName("GetInventoryProductBenchmark")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces<InventoryProductBenchmarkView>()
            .WithQueryProblems();
        endpoints.MapSupplierClaimEndpoints();
        endpoints.MapInventoryReleaseEndpoints();
        return endpoints;
    }

    private static async Task<IResult> CreateImportAsync(
        Guid tenantId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, InventorySupplierAccessPolicy supplierAccess, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await supplierAccess.EnsureUploadAccessAsync(
            identity.ActorId, new TenantId(tenantId), null, cancellationToken);
        var (form, source) = await InventoryUploadBody.ReadAsync(context, cancellationToken);
        var supplierIdValue = form["supplierId"].ToString();
        Guid? supplierId = null;
        if (!string.IsNullOrWhiteSpace(supplierIdValue))
        {
            if (!Guid.TryParse(supplierIdValue, out var parsedSupplierId))
            {
                throw new BadHttpRequestException(
                    "The selected supplier identifier is invalid.");
            }
            supplierId = parsedSupplierId;
        }
        var command = new CreateInventoryImportCommand(
            form["supplierName"].ToString(),
            source,
            supplierId);
        return await CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, requireVersion: false,
            commands.CreateAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/inventory-imports/{result.Data.Id}",
                result.Data),
            cancellationToken);
    }

    private static Task<IResult> ExecuteImportAsync(
        Guid tenantId, Guid importId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, new ExecuteInventoryImportCommand(), context, identity, clock,
            true, (envelope, token) => commands.ExecuteAsync(importId, envelope, token),
            cancellationToken);

    private static Task<IResult> RetryExtractionAsync(
        Guid tenantId, Guid importId, RetryInventoryExtractionCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.RetryExtractionAsync(importId, envelope, token),
            cancellationToken);

    private static Task<IResult> CancelExtractionAsync(
        Guid tenantId, Guid importId, CancelInventoryExtractionCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.CancelExtractionAsync(importId, envelope, token),
            cancellationToken);

    private static Task<IResult> ReconcileExtractionAsync(
        Guid tenantId, Guid importId, ReconcileInventoryExtractionCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReconcileExtractionAsync(importId, envelope, token),
            cancellationToken);

    private static Task<IResult> ReprojectExtractionAsync(
        Guid tenantId,
        Guid importId,
        ReprojectInventoryExtractionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventoryCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            tenantId, command, context, identity, clock,
            true,
            (envelope, token) =>
                commands.ReprojectExtractionAsync(
                    importId, envelope, token),
            cancellationToken);

    private static async Task<IResult> GetImportAsync(
        Guid tenantId, Guid importId, int? pageSize, string? cursor,
        HttpContext context, ICurrentIdentity identity,
        IInventoryReader reader, CancellationToken cancellationToken)
    {
        var result = await reader.GetImportAsync(
            identity.ActorId, new TenantId(tenantId), importId,
            pageSize ?? 100, cursor, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version);
        return Results.Ok(result);
    }

    private static Task<InventorySemanticPreflightView>
        GetSemanticPreflightAsync(
            Guid tenantId,
            Guid? importId,
            ICurrentIdentity identity,
            IInventorySemanticPreflightReader reader,
            CancellationToken cancellationToken) =>
        reader.GetAsync(
            identity.ActorId,
            new TenantId(tenantId),
            importId,
            cancellationToken);

    private static Task<IResult> ReviewCandidateAsync(
        Guid tenantId, Guid candidateId, ReviewInventoryCandidateCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReviewAsync(candidateId, envelope, token),
            cancellationToken);

    private static Task<IResult> PublishImportAsync(
        Guid tenantId, Guid importId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, new PublishInventoryImportCommand(), context, identity, clock,
            true, (envelope, token) => commands.PublishAsync(importId, envelope, token),
            cancellationToken);

    private static async Task<IResult> SearchProductsAsync(
        Guid tenantId, string? search, string? channel, string? supplier, string? geography,
        int? pageSize, string? cursor, ICurrentIdentity identity, IInventoryReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.SearchAsync(
            identity.ActorId, new TenantId(tenantId), new InventorySearchQuery(
                search, channel, supplier, geography, pageSize ?? 50, cursor),
            cancellationToken));

    private static async Task<IResult> GetProductAsync(
        Guid tenantId, Guid productId, ICurrentIdentity identity, IInventoryReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.GetProductAsync(
            identity.ActorId, new TenantId(tenantId), productId, cancellationToken));

    private static async Task<IResult> GetBenchmarkAsync(
        Guid tenantId, Guid productId, ICurrentIdentity identity,
        IInventoryBenchmarkReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetBenchmarkAsync(
            identity.ActorId, new TenantId(tenantId), productId, cancellationToken));

    private static Task<IResult> ReviewAssetRightsAsync(
        Guid tenantId, Guid assetId, ReviewInventoryAssetRightsCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReviewAssetRightsAsync(assetId, envelope, token),
            cancellationToken);

    private static async Task<IResult> UploadAssetAsync(
        Guid tenantId, Guid productId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, InventorySupplierAccessPolicy supplierAccess, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await supplierAccess.EnsureUploadAccessAsync(
            identity.ActorId, new TenantId(tenantId), productId, cancellationToken);
        var (form, source) = await InventoryUploadBody.ReadAsync(context, cancellationToken);
        var versionText = form["productVersionId"].ToString();
        if (!Guid.TryParse(versionText, out var productVersionId))
            throw new BadHttpRequestException("The current product version is required.");
        var command = new UploadInventoryAssetCommand(
            productVersionId, form["assetType"].ToString(),
            source);
        return await CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, requireVersion: true,
            (envelope, token) => commands.UploadAssetAsync(productId, envelope, token),
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/inventory-assets/{result.Data.AssetId}",
                result.Data), cancellationToken);
    }

    private static Task<IResult> RecordAvailabilityExceptionAsync(
        Guid tenantId,
        Guid productId,
        RecordInventoryAvailabilityExceptionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventoryCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.RecordAvailabilityExceptionAsync(
                productId, envelope, token), cancellationToken);

    private static async Task<IResult> GetImportSourceAsync(Guid tenantId, Guid importId,
        HttpContext context, ICurrentIdentity identity, IInventoryReader reader, CancellationToken cancellationToken)
    {
        var source = await reader.GetImportSourceAsync(identity.ActorId, new TenantId(tenantId), importId, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        return Results.File(source.Content, "application/octet-stream", source.FileName);
    }

    private static async Task<IResult> GetApprovedAssetAsync(
        Guid tenantId, Guid assetId, ICurrentIdentity identity,
        IInventoryReader reader, CancellationToken cancellationToken)
    {
        var asset = await reader.GetApprovedAssetAsync(
            identity.ActorId, new TenantId(tenantId), assetId, cancellationToken);
        return Results.File(asset.Content, asset.MediaType,
            enableRangeProcessing: false);
    }

    private static Task<IResult> SubmitEmbeddingAsync(
        Guid tenantId, Guid productId, SubmitInventoryEmbeddingCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.SubmitEmbeddingAsync(productId, envelope, token),
            cancellationToken);

    private static async Task<IResult> GetSemanticRecallAsync(
        Guid tenantId, Guid productId, int? limit, ICurrentIdentity identity,
        IInventoryReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetSemanticRecallAsync(
            identity.ActorId, new TenantId(tenantId), productId, limit ?? 10,
            cancellationToken));

    private static Task<IResult> NominateSemanticDuplicateAsync(
        Guid tenantId, Guid productId, NominateInventorySemanticDuplicateCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.NominateSemanticDuplicateAsync(
                productId, envelope, token),
            cancellationToken);

    private static async Task<IResult> ListDuplicateCandidatesAsync(
        Guid tenantId, string? status, ICurrentIdentity identity,
        IInventoryReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListDuplicateCandidatesAsync(
            identity.ActorId, new TenantId(tenantId), status, cancellationToken));

    private static Task<IResult> ReviewDuplicateAsync(
        Guid tenantId, Guid candidateId, ReviewInventoryDuplicateCommand command,
        HttpContext context, ICurrentIdentity identity, IInventoryCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReviewDuplicateAsync(candidateId, envelope, token),
            cancellationToken);

    private static Task<IResult> ExecuteAsync<TCommand, TResult>(
        Guid tenantId, TCommand command, HttpContext context, ICurrentIdentity identity,
        TimeProvider clock, bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken,
            Task<Advertified.Commercial.Application.Foundation.CommandResult<TResult>>> action,
        CancellationToken cancellationToken)
        where TCommand : notnull where TResult : notnull =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock,
            requireVersion, action, cancellationToken);
}
