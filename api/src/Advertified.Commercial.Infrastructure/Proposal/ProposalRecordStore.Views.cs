using System.Text.Json;

using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalRecordStore
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal async Task<ProposalVersionView> BuildViewAsync(
        TenantId tenantId,
        ProposalRow proposal,
        CancellationToken cancellationToken)
    {
        var options = await ListOptionsAsync(tenantId, proposal.Id, cancellationToken);
        var document = await FindDocumentAsync(tenantId, proposal.Id, cancellationToken);
        var decision = await FindDecisionAsync(tenantId, proposal.Id, cancellationToken);
        var impacts = await ListInventoryImpactsAsync(
            tenantId, proposal.Id, cancellationToken);
        return new ProposalVersionView(
            proposal.Id, proposal.BriefId, proposal.BriefVersionId, proposal.VersionNumber,
            proposal.Title, proposal.ExecutiveSummary, proposal.Terms, proposal.ExpiryAtUtc,
            proposal.Status, options.Select(ToOptionView).ToArray(),
            document is null ? null : new ProposalDocumentView(
                document.Id, document.MediaType, document.ContentHash,
                document.Content.LongLength, document.CreatedAtUtc),
            proposal.RecipientUserId,
            decision is null ? null : new ProposalDecisionView(
                decision.Decision, decision.OptionId, decision.Reason,
                decision.DecidedBy, decision.DecidedAtUtc,
                decision.RecordedForExternalParty, decision.ExternalPartyEmail,
                decision.EvidenceReference),
            proposal.CreatedBy, proposal.ApprovedBy, proposal.ApprovalMode,
            proposal.ApprovalAssigneeUserId, proposal.ApprovalRequestedBy,
            proposal.ApprovalRequestedAtUtc, proposal.ApprovalRejectedBy,
            proposal.ApprovalRejectionReason, proposal.ApprovalRejectedAtUtc,
            proposal.InventoryReviewStatus, impacts,
            proposal.Version, proposal.CreatedAtUtc);
    }

    private static ProposalOptionView ToOptionView(ProposalOptionRow row)
    {
        var inventory = ReadInventory(row.InventoryJson);
        return new ProposalOptionView(
            row.Id, row.Label, row.Outcome, row.PlanVersionId, row.PlanVersionNumber,
            row.BudgetMinor, row.Currency, row.DisplayOrder,
            Read<string[]>(row.ChannelsJson),
            Read<ProposalRunningPeriodView[]>(row.RunningPeriodsJson),
            inventory.Names, inventory.Lines);
    }

    internal static ProposalInventorySnapshot ReadInventory(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array ||
            document.RootElement.GetArrayLength() == 0)
        {
            return new([], []);
        }
        if (document.RootElement[0].ValueKind == JsonValueKind.String)
        {
            return new(Read<string[]>(json), []);
        }
        var lines = Read<ProposalInventoryLineView[]>(json);
        return new(lines.Select(item => item.Name).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray(), lines);
    }

    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, StoredJson);

    internal static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored proposal JSON is invalid.");
}

internal sealed record ProposalInventorySnapshot(
    IReadOnlyList<string> Names,
    IReadOnlyList<ProposalInventoryLineView> Lines);
