using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationRecordStore
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static InboundMailboxView ToView(InboundMailboxRow row) => new(
        row.Id,
        row.TenantId,
        row.Address,
        row.Provider,
        row.OwnerUserId,
        row.DefaultClientAccountId,
        row.AutoSendEnabled,
        Read<string[]>(row.AllowedSenderDomainsJson),
        row.IsEnabled,
        row.Version,
        row.UpdatedAtUtc);

    internal async Task<InboundCampaignEmailView> BuildEmailViewAsync(
        TenantId tenantId,
        InboundCampaignEmailRow row,
        string status,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        var attachments = await ListAttachmentsAsync(
            tenantId, row.Id, cancellationToken);
        return new InboundCampaignEmailView(
            row.Id,
            row.TenantId,
            row.MailboxId,
            row.ProviderEmailId,
            row.ProviderMessageId,
            row.SenderEmail,
            row.SenderName,
            row.ReplyToEmail,
            row.Subject,
            row.SourceHash,
            attachments.Select(ToView).ToArray(),
            status,
            failureCode,
            row.ReceivedAtUtc,
            row.CreatedAtUtc);
    }

    internal static EmailAutomationRunView ToView(EmailAutomationRunRow row) => new(
        row.Id,
        row.TenantId,
        row.InboundEmailId,
        row.CampaignMode,
        row.Status,
        row.Checkpoint,
        row.ClientAccountId,
        row.BriefId,
        row.BriefVersionId,
        row.StpVersionId,
        row.MediaMixVersionId,
        row.ShortlistVersionId,
        row.MediaPlanVersionId,
        row.ProposalVersionId,
        row.DocumentId,
        row.FailureCode,
        row.FailureMessage,
        row.DeliveryProviderId,
        row.IncrementalAiCostMinor,
        row.Version,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    internal static string Write<T>(T value) =>
        JsonSerializer.Serialize(value, StoredJson);

    internal static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored email automation JSON is invalid.");

    internal static string? EncodeCursor(DateTimeOffset? value) =>
        value.HasValue
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Value.ToString("O")))
            : null;

    internal static DateTimeOffset? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }
        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The email automation cursor is invalid.",
                nameof(cursor), exception);
        }
    }

    private static InboundAttachmentView ToView(InboundAttachmentRow row) => new(
        row.ProviderAttachmentId,
        row.FileName,
        row.MediaType,
        row.SizeBytes);
}
