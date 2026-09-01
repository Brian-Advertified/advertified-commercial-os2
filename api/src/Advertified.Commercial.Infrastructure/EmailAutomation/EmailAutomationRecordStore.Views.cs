using System.Globalization;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationRecordStore
{
    private const int MaximumCursorLength = 256;
    private const string CursorVersion = "v1";
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
        return BuildEmailView(row, status, failureCode, attachments);
    }

    internal static InboundCampaignEmailView BuildEmailView(
        InboundCampaignEmailRow row,
        string status,
        string? failureCode,
        IReadOnlyList<InboundAttachmentRow> attachments) => new(
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
        row.DeliveryProviderCode,
        row.DeliveryProviderId,
        row.DeliveryRequestedAtUtc,
        row.DeliveryAcceptedAtUtc,
        row.IncrementalAiCostMinor,
        row.Version,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    internal static string Write<T>(T value) =>
        JsonSerializer.Serialize(value, StoredJson);

    internal static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored email automation JSON is invalid.");

    internal static string EncodeCursor(DateTimeOffset receivedAtUtc, Guid id)
    {
        var value = string.Join('|',
            CursorVersion,
            receivedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            id.ToString("D", CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    internal static EmailAutomationCursorValue? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length > MaximumCursorLength) throw InvalidCursor();
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 3 || parts[0] != CursorVersion ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var ticks) ||
                !Guid.TryParse(parts[2], out var id) || id == Guid.Empty)
            {
                throw new FormatException();
            }
            return new EmailAutomationCursorValue(
                new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentOutOfRangeException)
        {
            throw InvalidCursor(exception);
        }
    }

    private static ArgumentException InvalidCursor(Exception? inner = null) =>
        new("The email automation cursor is invalid.", inner);

    private static InboundAttachmentView ToView(InboundAttachmentRow row) => new(
        row.ProviderAttachmentId,
        row.FileName,
        row.MediaType,
        row.SizeBytes);
}

internal sealed record EmailAutomationCursorValue(
    DateTimeOffset ReceivedAtUtc,
    Guid Id);
