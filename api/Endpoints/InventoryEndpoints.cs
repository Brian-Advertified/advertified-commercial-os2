using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Inventory truth").RequireAuthorization();
        group.MapPost("/inventory-imports", CreateImportAsync)
            .WithName("CreateInventoryImport")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<InventoryImportView>(StatusCodes.Status201Created)
            .WithGate4CommandProblems(requiresVersion: false);
        group.MapPost("/inventory-imports/{importId:guid}:execute", ExecuteImportAsync)
            .WithName("ExecuteInventoryImport").Produces<InventoryImportView>()
            .WithGate4CommandProblems(requiresVersion: true);
        group.MapGet("/inventory-imports/{importId:guid}", GetImportAsync)
            .WithName("GetInventoryImport").Produces<InventoryImportView>()
            .WithGate4QueryProblems();
        group.MapPost("/inventory-candidates/{candidateId:guid}:review", ReviewCandidateAsync)
            .WithName("ReviewInventoryCandidate").Produces<InventoryCandidateView>()
            .WithGate4CommandProblems(requiresVersion: true);
        group.MapPost("/inventory-imports/{importId:guid}:publish", PublishImportAsync)
            .WithName("PublishInventoryImport").Produces<InventoryImportView>()
            .WithGate4CommandProblems(requiresVersion: true);
        group.MapGet("/inventory-products", SearchProductsAsync)
            .WithName("SearchInventoryProducts").Produces<InventoryProductPage>()
            .WithGate4QueryProblems();
        group.MapGet("/inventory-products/{productId:guid}", GetProductAsync)
            .WithName("GetInventoryProduct").Produces<InventoryProductView>()
            .WithGate4QueryProblems();
        return endpoints;
    }

    private static async Task<IResult> CreateImportAsync(
        Guid tenantId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("source")
            ?? throw new BadHttpRequestException("An inventory source file is required.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var command = new CreateInventoryImportCommand(
            form["supplierName"].ToString(),
            new InventorySourceFile(file.FileName, file.ContentType, stream.ToArray()));
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId, command, clock, false);
        var result = await commands.CreateAsync(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return Results.Created(
            $"/api/v1/tenants/{tenantId}/inventory-imports/{result.Data.Id}", result.Data);
    }

    private static Task<IResult> ExecuteImportAsync(
        Guid tenantId, Guid importId, HttpContext context, ICurrentIdentity identity,
        IInventoryCommands commands, TimeProvider clock, CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, new ExecuteInventoryImportCommand(), context, identity, clock,
            true, (envelope, token) => commands.ExecuteAsync(importId, envelope, token),
            cancellationToken);

    private static async Task<IResult> GetImportAsync(
        Guid tenantId, Guid importId, HttpContext context, ICurrentIdentity identity,
        IInventoryReader reader, CancellationToken cancellationToken)
    {
        var result = await reader.GetImportAsync(
            identity.ActorId, new TenantId(tenantId), importId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version);
        return Results.Ok(result);
    }

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

    private static async Task<IResult> ExecuteAsync<TCommand, TResult>(
        Guid tenantId, TCommand command, HttpContext context, ICurrentIdentity identity,
        TimeProvider clock, bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken,
            Task<Advertified.Commercial.Application.Foundation.CommandResult<TResult>>> action,
        CancellationToken cancellationToken)
        where TCommand : notnull where TResult : notnull
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId, command, clock, requireVersion);
        var result = await action(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return Results.Ok(result.Data);
    }
}
