using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class ResendEmailProviderClient(
    HttpClient httpClient,
    IOptions<EmailAutomationOptions> options,
    TimeProvider timeProvider) : IEmailProviderClient
{
    private const string ReceivedEventType = "email.received";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly EmailAutomationOptions configuration = options.Value;

    public string ProviderCode => MasterDataCodes.EmailProviders.Resend;

    public InboundEmailIdentityAssessment AssessInboundIdentity(
        RetrievedInboundEmail email) => new(false);

    public bool VerifyWebhook(
        string rawPayload,
        string messageId,
        string timestamp,
        string signature,
        DateTimeOffset now) =>
        !string.IsNullOrWhiteSpace(configuration.ResendWebhookSecret) &&
        ResendWebhookVerifier.Verify(
            rawPayload,
            messageId,
            timestamp,
            signature,
            configuration.ResendWebhookSecret,
            TimeSpan.FromSeconds(configuration.WebhookToleranceSeconds),
            now);

    public InboundEmailNotification ParseNotification(string rawPayload)
    {
        var notification = JsonSerializer.Deserialize<ResendWebhookEvent>(rawPayload, Json)
            ?? throw new InvalidEmailWebhookException();
        if (!string.Equals(notification.Type, ReceivedEventType, StringComparison.Ordinal) ||
            notification.Data is null ||
            string.IsNullOrWhiteSpace(notification.Data.EmailId) ||
            string.IsNullOrWhiteSpace(notification.Data.MessageId))
        {
            throw new InvalidEmailWebhookException();
        }
        return new InboundEmailNotification(
            ProviderCode,
            notification.Data.EmailId,
            notification.Data.MessageId,
            notification.Data.To ?? [],
            notification.Data.From ?? string.Empty,
            notification.Data.Subject ?? string.Empty,
            notification.Data.CreatedAt,
            ToReferences(notification.Data.Attachments));
    }

    public async Task<RetrievedInboundEmail> RetrieveAsync(
        string providerEmailId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"emails/receiving/{Uri.EscapeDataString(providerEmailId)}?html_format=cid");
        using var response = await SendRequestAsync(
            request,
            delivery: false,
            cancellationToken);
        ResendReceivedEmail message;
        try
        {
            message = await response.Content.ReadFromJsonAsync<ResendReceivedEmail>(
                Json, cancellationToken)
                ?? throw new EmailPayloadUnavailableException();
        }
        catch (JsonException exception)
        {
            throw new EmailPayloadUnavailableException(exception);
        }
        var sender = ParseAddress(message.From);
        return new RetrievedInboundEmail(
            message.Id,
            message.MessageId,
            message.To ?? [],
            sender.Address,
            sender.DisplayName,
            message.ReplyTo ?? [],
            message.Subject ?? string.Empty,
            message.Text,
            message.Html,
            message.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ToReferences(message.Attachments),
            message.CreatedAt);
    }

    public async Task<EmailDeliveryReceipt> SendAsync(
        ProposalEmailDelivery delivery,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "emails");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", delivery.IdempotencyKey);
        var headers = string.IsNullOrWhiteSpace(delivery.InReplyTo)
            ? null
            : new Dictionary<string, string> { ["In-Reply-To"] = delivery.InReplyTo };
        request.Content = JsonContent.Create(new ResendSendEmail(
            delivery.From,
            [delivery.To],
            delivery.Subject,
            delivery.TextBody,
            headers,
            [new ResendSendAttachment(
                delivery.FileName,
                Convert.ToBase64String(delivery.Attachment))]),
            options: Json);
        using var response = await SendRequestAsync(
            request,
            delivery: true,
            cancellationToken);
        ResendSendResult? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<ResendSendResult>(
                Json, cancellationToken);
        }
        catch (Exception exception) when (IsAmbiguousDeliveryResponse(exception))
        {
            throw new EmailDeliveryAcceptanceUnknownException(exception);
        }
        if (string.IsNullOrWhiteSpace(result?.Id))
        {
            throw new EmailDeliveryAcceptanceUnknownException();
        }
        return new EmailDeliveryReceipt(result.Id, timeProvider.GetUtcNow());
    }

    public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new EmailDeliveryReconciliationResult(
            EmailDeliveryReconciliationOutcome.Unknown,
            null));
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        bool delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }
            response.Dispose();
            if (delivery)
            {
                if ((int)response.StatusCode >= 500 ||
                    response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    throw new EmailDeliveryAcceptanceUnknownException();
                }
                throw new EmailDeliveryFailedException();
            }
            throw new EmailProviderUnavailableException();
        }
        catch (OperationCanceledException exception) when (delivery)
        {
            throw new EmailDeliveryAcceptanceUnknownException(exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailProviderUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw delivery
                ? new EmailDeliveryAcceptanceUnknownException(exception)
                : new EmailProviderUnavailableException(exception);
        }
    }

    private static bool IsAmbiguousDeliveryResponse(Exception exception) =>
        exception is JsonException or NotSupportedException or HttpRequestException or
            IOException or OperationCanceledException;

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(configuration.ResendApiKey))
        {
            throw new EmailProviderUnavailableException();
        }
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", configuration.ResendApiKey);
        return request;
    }

    private static InboundAttachmentReference[] ToReferences(
        IReadOnlyList<ResendAttachment>? attachments) =>
        (attachments ?? []).Select(item => new InboundAttachmentReference(
            item.Id,
            item.FileName,
            item.ContentType,
            item.Size)).ToArray();

    private static (string Address, string? DisplayName) ParseAddress(string? value)
    {
        try
        {
            var address = new MailAddress(value ?? string.Empty);
            return (address.Address, string.IsNullOrWhiteSpace(address.DisplayName)
                ? null : address.DisplayName);
        }
        catch (FormatException)
        {
            return (value?.Trim() ?? string.Empty, null);
        }
    }

    private sealed record ResendWebhookEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] ResendEmailData? Data);

    private sealed record ResendEmailData(
        [property: JsonPropertyName("email_id")] string EmailId,
        [property: JsonPropertyName("message_id")] string MessageId,
        [property: JsonPropertyName("from")] string? From,
        [property: JsonPropertyName("to")] string[]? To,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("attachments")] ResendAttachment[]? Attachments);

    private sealed record ResendReceivedEmail(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("message_id")] string MessageId,
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[]? To,
        [property: JsonPropertyName("reply_to")] string[]? ReplyTo,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("html")] string? Html,
        [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers,
        [property: JsonPropertyName("attachments")] ResendAttachment[]? Attachments,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

    private sealed record ResendAttachment(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("filename")] string FileName,
        [property: JsonPropertyName("content_type")] string ContentType,
        [property: JsonPropertyName("size")] long Size = 0);

    private sealed record ResendSendEmail(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers,
        [property: JsonPropertyName("attachments")] ResendSendAttachment[] Attachments);

    private sealed record ResendSendAttachment(
        [property: JsonPropertyName("filename")] string FileName,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResendSendResult(
        [property: JsonPropertyName("id")] string Id);
}
