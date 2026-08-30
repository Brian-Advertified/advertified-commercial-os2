using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

internal static partial class EmailContentNormalizer
{
    internal static string NormalizeAddress(string value)
    {
        try
        {
            return new MailAddress(value.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new EmailAutomationReviewRequiredException(
                Advertified.Commercial.Domain.MasterData.MasterDataCodes
                    .AutomationFailureReasons.InvalidRecipient,
                "The sender or reply address is not valid.");
        }
    }

    internal static string SelectReplyAddress(RetrievedInboundEmail email)
    {
        foreach (var value in email.ReplyTo.Append(email.SenderEmail))
        {
            try
            {
                return new MailAddress(value.Trim()).Address.ToLowerInvariant();
            }
            catch (FormatException)
            {
                // Try the next address without disclosing the rejected value.
            }
        }
        throw new EmailAutomationReviewRequiredException(
            Advertified.Commercial.Domain.MasterData.MasterDataCodes
                .AutomationFailureReasons.InvalidRecipient,
            "The inbound request has no valid reply address.");
    }

    internal static string Body(RetrievedInboundEmail email)
    {
        if (!string.IsNullOrWhiteSpace(email.TextBody))
        {
            return NormalizeWhitespace(email.TextBody);
        }
        if (string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            return string.Empty;
        }
        var withoutMarkup = HtmlTag().Replace(email.HtmlBody, " ");
        return NormalizeWhitespace(WebUtility.HtmlDecode(withoutMarkup));
    }

    internal static string SourceHash(
        RetrievedInboundEmail email,
        string sender,
        string replyTo,
        string body)
    {
        var attachmentSignature = string.Join('|', email.Attachments
            .OrderBy(item => item.ProviderAttachmentId, StringComparer.Ordinal)
            .Select(item => string.Join(':', item.ProviderAttachmentId,
                item.FileName, item.MediaType, item.SizeBytes)));
        return OpportunityCommandSupport.Hash(string.Join('\n',
            sender,
            replyTo,
            email.Subject.Trim(),
            body,
            attachmentSignature));
    }

    internal static string Metadata(
        string rawPayload,
        RetrievedInboundEmail email)
    {
        using var notification = JsonDocument.Parse(rawPayload);
        return JsonSerializer.Serialize(new
        {
            notification = notification.RootElement,
            headers = email.Headers,
            recipients = email.Recipients,
        });
    }

    private static string NormalizeWhitespace(string value)
    {
        var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => InlineWhitespace().Replace(line.Trim(), " "))
            .Where(line => line.Length > 0);
        return string.Join('\n', lines);
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();

    [GeneratedRegex("[\\t ]+", RegexOptions.CultureInvariant)]
    private static partial Regex InlineWhitespace();
}
