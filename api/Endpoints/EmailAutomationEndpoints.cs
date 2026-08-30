using System.Text;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Endpoints;

public static class EmailAutomationEndpoints
{
    private const string WebhookIdHeader = "svix-id";
    private const string WebhookTimestampHeader = "svix-timestamp";
    private const string WebhookSignatureHeader = "svix-signature";

    public static IEndpointRouteBuilder MapEmailAutomationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/email-automation")
            .WithTags("Inbound OOH proposal automation")
            .RequireAuthorization();

        group.MapPost("/mailbox", CreateMailboxAsync)
            .WithName("CreateInboundProposalMailbox")
            .Produces<InboundMailboxView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPut("/mailbox", UpdateMailboxAsync)
            .WithName("UpdateInboundProposalMailbox")
            .Produces<InboundMailboxView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/mailbox", GetMailboxAsync)
            .WithName("GetInboundProposalMailbox")
            .Produces<InboundMailboxView>()
            .WithQueryProblems();
        group.MapGet("/messages", ListMessagesAsync)
            .WithName("ListInboundProposalMessages")
            .Produces<InboundEmailPage>()
            .WithQueryProblems();
        group.MapGet("/messages/{inboundEmailId:guid}", GetMessageAsync)
            .WithName("GetInboundProposalMessage")
            .Produces<InboundEmailDetailView>()
            .WithQueryProblems();
        group.MapPost("/messages/{inboundEmailId:guid}:process", ProcessMessageAsync)
            .WithName("ProcessInboundProposalMessage")
            .Produces<EmailAutomationRunView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/messages/{inboundEmailId:guid}:retry", RetryMessageAsync)
            .WithName("RetryInboundProposalMessage")
            .Produces<EmailAutomationRunView>()
            .WithCommandProblems(requiresVersion: true);

        endpoints.MapPost(
                "/api/v1/tenants/{tenantId:guid}/email-automation/webhooks/{provider}",
                ReceiveWebhookAsync)
            .AllowAnonymous()
            .WithTags("Inbound OOH proposal automation")
            .WithName("ReceiveInboundProposalWebhook")
            .Produces<InboundEmailReceiptView>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        return endpoints;
    }

    private static Task<IResult> CreateMailboxAsync(
        Guid tenantId,
        ConfigureInboundMailboxCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ConfigureMailboxAsync(
            tenantId, command, context, identity, commands, timeProvider,
            requireVersion: false, StatusCodes.Status201Created, cancellationToken);

    private static Task<IResult> UpdateMailboxAsync(
        Guid tenantId,
        ConfigureInboundMailboxCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ConfigureMailboxAsync(
            tenantId, command, context, identity, commands, timeProvider,
            requireVersion: true, StatusCodes.Status200OK, cancellationToken);

    private static async Task<IResult> ConfigureMailboxAsync(
        Guid tenantId,
        ConfigureInboundMailboxCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        bool requireVersion,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId, command,
            timeProvider, requireVersion);
        var result = await commands.ConfigureMailboxAsync(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return Results.Json(result.Data, statusCode: statusCode);
    }

    private static async Task<IResult> GetMailboxAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IEmailAutomationReader reader,
        CancellationToken cancellationToken)
    {
        var mailbox = await reader.GetMailboxAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken);
        return Results.Ok(mailbox);
    }

    private static async Task<IResult> ListMessagesAsync(
        Guid tenantId,
        int? pageSize,
        string? cursor,
        ICurrentIdentity identity,
        IEmailAutomationReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.ListAsync(
            identity.ActorId,
            new TenantId(tenantId),
            pageSize ?? 25,
            cursor,
            cancellationToken));

    private static async Task<IResult> GetMessageAsync(
        Guid tenantId,
        Guid inboundEmailId,
        ICurrentIdentity identity,
        IEmailAutomationReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.GetAsync(
            identity.ActorId,
            new TenantId(tenantId),
            inboundEmailId,
            cancellationToken));

    private static Task<IResult> ProcessMessageAsync(
        Guid tenantId,
        Guid inboundEmailId,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteAsync(
            tenantId,
            inboundEmailId,
            new ProcessInboundEmailCommand(),
            context,
            identity,
            commands,
            timeProvider,
            requireVersion: false,
            (service, id, envelope, token) => service.ProcessAsync(id, envelope, token),
            cancellationToken);

    private static Task<IResult> RetryMessageAsync(
        Guid tenantId,
        Guid inboundEmailId,
        RetryInboundEmailCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteAsync(
            tenantId,
            inboundEmailId,
            command,
            context,
            identity,
            commands,
            timeProvider,
            requireVersion: true,
            (service, id, envelope, token) => service.RetryAsync(id, envelope, token),
            cancellationToken);

    private static async Task<IResult> ExecuteAsync<TCommand>(
        Guid tenantId,
        Guid inboundEmailId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IEmailAutomationCommands commands,
        TimeProvider timeProvider,
        bool requireVersion,
        Func<IEmailAutomationCommands, Guid, CommandEnvelope<TCommand>,
            CancellationToken,
            Task<Advertified.Commercial.Application.Foundation.CommandResult<EmailAutomationRunView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId, command,
            timeProvider, requireVersion);
        var result = await execute(commands, inboundEmailId, envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> ReceiveWebhookAsync(
        Guid tenantId,
        string provider,
        HttpContext context,
        IEmailProviderResolver providers,
        IInboundEmailReceiver receiver,
        IOptions<EmailAutomationOptions> options,
        EmailAutomationPolicy policy,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (options.Value.Mode == EmailAutomationOptions.DisabledMode)
        {
            throw new InboundMailboxNotConfiguredException();
        }
        var rawPayload = await ReadBodyAsync(
            context.Request, policy.MaximumSourceBytes, cancellationToken);
        var providerClient = providers.Resolve(provider);
        var webhookId = RequiredHeader(context, WebhookIdHeader);
        var timestamp = RequiredHeader(context, WebhookTimestampHeader);
        var signature = RequiredHeader(context, WebhookSignatureHeader);
        if (!providerClient.VerifyWebhook(
                rawPayload, webhookId, timestamp, signature, timeProvider.GetUtcNow()))
        {
            throw new InvalidEmailWebhookException();
        }
        var notification = providerClient.ParseNotification(rawPayload);
        if (!string.Equals(
                notification.Provider, providerClient.ProviderCode, StringComparison.Ordinal))
        {
            throw new InvalidEmailWebhookException();
        }
        var receipt = await receiver.ReceiveAsync(
            new TenantId(tenantId),
            notification,
            webhookId,
            rawPayload,
            new CorrelationId(Guid.Parse(context.TraceIdentifier)),
            cancellationToken);
        return Results.Ok(receipt);
    }

    private static string RequiredHeader(HttpContext context, string name)
    {
        var value = context.Request.Headers[name].ToString().Trim();
        return value.Length > 0 ? value : throw new InvalidEmailWebhookException();
    }

    private static async Task<string> ReadBodyAsync(
        HttpRequest request,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > maximumBytes)
        {
            throw new BadHttpRequestException(
                "The inbound email notification is too large.",
                StatusCodes.Status413PayloadTooLarge);
        }
        await using var buffer = new MemoryStream();
        var block = new byte[16_384];
        while (true)
        {
            var read = await request.Body.ReadAsync(block, cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > maximumBytes)
            {
                throw new BadHttpRequestException(
                    "The inbound email notification is too large.",
                    StatusCodes.Status413PayloadTooLarge);
            }
            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken);
        }
        try
        {
            return new UTF8Encoding(false, true).GetString(buffer.ToArray());
        }
        catch (DecoderFallbackException exception)
        {
            throw new BadHttpRequestException(
                "The inbound email notification must be valid UTF-8.", exception);
        }
    }
}
