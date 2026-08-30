using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class FundingEndpoints
{
    public static IEndpointRouteBuilder MapFundingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Funding").RequireAuthorization();
        group.MapGet("/funding", GetWorkspaceAsync)
            .WithName("GetFundingWorkspace").Produces<FundingWorkspaceView>()
            .WithQueryProblems();
        group.MapPost("/purchase-orders", SubmitPurchaseOrderAsync)
            .WithName("SubmitPurchaseOrder")
            .Accepts<SubmitPurchaseOrderForm>("multipart/form-data")
            .Produces<PurchaseOrderView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}:approve", ApprovePurchaseOrderAsync)
            .WithName("ApprovePurchaseOrder").Produces<PurchaseOrderView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/invoices:issue", IssueInvoiceAsync)
            .WithName("IssueInvoice").Produces<InvoiceView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/payment-intents", StartPaymentAsync)
            .WithName("StartPayment").Produces<PaymentIntentView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/payment-intents/{paymentIntentId:guid}:reconcile", ReconcilePaymentAsync)
            .WithName("ReconcilePayment")
            .Accepts<ReconcilePaymentForm>("multipart/form-data")
            .Produces<PaymentIntentView>().WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> GetWorkspaceAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IFundingReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetWorkspaceAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> SubmitPurchaseOrderAsync(
        Guid tenantId,
        HttpContext context,
        ICurrentIdentity identity,
        IFundingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var command = new SubmitPurchaseOrderCommand(
            RequiredGuid(form, "proposalVersionId"),
            RequiredGuid(form, "proposalOptionId"),
            form["purchaseOrderNumber"].ToString(),
            RequiredLong(form, "amountMinor"),
            form["currency"].ToString(),
            await ReadDocumentAsync(form, "document", cancellationToken));
        return await CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, false,
            commands.SubmitPurchaseOrderAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/purchase-orders/{result.Data.Id}", result.Data),
            cancellationToken);
    }

    private static Task<IResult> ApprovePurchaseOrderAsync(
        Guid tenantId,
        Guid purchaseOrderId,
        ApprovePurchaseOrderCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IFundingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ApprovePurchaseOrderAsync(
                purchaseOrderId, envelope, token), cancellationToken);

    private static Task<IResult> IssueInvoiceAsync(
        Guid tenantId,
        IssueInvoiceCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IFundingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, false,
            commands.IssueInvoiceAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/invoices/{result.Data.Id}", result.Data),
            cancellationToken);

    private static Task<IResult> StartPaymentAsync(
        Guid tenantId,
        StartPaymentCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IFundingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, false,
            commands.StartPaymentAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/payment-intents/{result.Data.Id}", result.Data),
            cancellationToken);

    private static async Task<IResult> ReconcilePaymentAsync(
        Guid tenantId,
        Guid paymentIntentId,
        HttpContext context,
        ICurrentIdentity identity,
        IFundingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var command = new ReconcilePaymentCommand(
            form["reconciliationReference"].ToString(),
            form["reason"].ToString(),
            await ReadDocumentAsync(form, "receipt", cancellationToken));
        return await CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReconcilePaymentAsync(
                paymentIntentId, envelope, token), cancellationToken);
    }

    private static Guid RequiredGuid(IFormCollection form, string name) =>
        Guid.TryParse(form[name].ToString(), out var value)
            ? value : throw new BadHttpRequestException($"{name} is invalid.");

    private static long RequiredLong(IFormCollection form, string name) =>
        long.TryParse(form[name].ToString(), out var value)
            ? value : throw new BadHttpRequestException($"{name} is invalid.");

    private static async Task<FundingDocument> ReadDocumentAsync(
        IFormCollection form,
        string name,
        CancellationToken cancellationToken)
    {
        var file = form.Files.GetFile(name)
            ?? throw new BadHttpRequestException($"{name} is required.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return new FundingDocument(file.FileName, file.ContentType, stream.ToArray());
    }
}

public sealed record SubmitPurchaseOrderForm(
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    string PurchaseOrderNumber,
    long AmountMinor,
    string Currency,
    IFormFile Document);

public sealed record ReconcilePaymentForm(
    string ReconciliationReference,
    string Reason,
    IFormFile Receipt);
