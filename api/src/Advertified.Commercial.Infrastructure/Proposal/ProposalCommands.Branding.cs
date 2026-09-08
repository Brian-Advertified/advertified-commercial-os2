using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private const int MaximumBrandAssetBytes = 2 * 1024 * 1024;

    private async Task<CommandOutcome> UploadBrandAssetOutcomeAsync(
        CommandEnvelope<UploadProposalBrandAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var label = OpportunityCommandSupport.Required(command.Label, 200, nameof(command.Label));
        var source = OpportunityCommandSupport.Required(
            command.SourceReference, 1000, nameof(command.SourceReference));
        var fileName = Path.GetFileName(OpportunityCommandSupport.Required(
            command.Document.FileName, 300, nameof(command.Document.FileName)));
        EnsureJpeg(command.Document);
        var proposal = await LoadOwnedProposalAsync(
            command.ProposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Draft)
            throw new InvalidLifecycleTransitionException();
        var brief = await store.FindPlanningReadyBriefAsync(
            envelope.TenantId, proposal.BriefId, cancellationToken)
            ?? throw new ProposalStaleException();
        var clientAccountId = command.ClientAsset ? brief.ClientAccountId : (Guid?)null;
        var scan = await malwareScanner.ScanAsync(command.Document.Content, cancellationToken);
        if (!scan.IsClean) throw new ArgumentException("The brand asset did not pass file safety checks.");
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var hash = Convert.ToHexStringLower(SHA256.HashData(command.Document.Content));
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.workspace_brand_assets (
                id, tenant_id, client_account_id, label, media_type, file_name,
                content_hash, content, source_reference, uploaded_by, version, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {clientAccountId}, {label},
                {"image/jpeg"}, {fileName}, {hash}, {command.Document.Content}, {source},
                {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        var row = await store.FindBrandAssetAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The brand asset was not persisted.");
        return OpportunityCommandSupport.Outcome(
            envelope, ProposalRecordStore.ToBrandAssetView(row), id, 1,
            MasterDataReferences.CommercialResourceTypes.CreativeAsset,
            MasterDataReferences.CommercialActions.CreativeAssetVersionUploaded,
            MasterDataReferences.CommercialEventTypes.CreativeAssetVersionUploaded, now);
    }

    private async Task<CommandOutcome> ApproveBrandAssetOutcomeAsync(
        Guid assetId,
        CommandEnvelope<ApproveProposalBrandAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var asset = await store.FindBrandAssetAsync(
            envelope.TenantId, assetId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brand asset access denied.");
        if (asset.ApprovedAtUtc is not null) throw new InvalidLifecycleTransitionException();
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.workspace_brand_assets
            SET approved_by = {envelope.ActorId.Value}, approved_at_utc = {now},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {assetId}
              AND approved_at_utc IS NULL AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = asset with
        {
            ApprovedBy = envelope.ActorId.Value,
            ApprovedAtUtc = now,
            Version = asset.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, ProposalRecordStore.ToBrandAssetView(updated), assetId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.CreativeAsset,
            MasterDataReferences.CommercialActions.CreativeAssetBrandReviewed,
            MasterDataReferences.CommercialEventTypes.CreativeAssetBrandReviewed, now);
    }

    private async Task<CommandOutcome> ConfigureBrandingOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<ConfigureProposalBrandingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(proposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Draft)
            throw new InvalidLifecycleTransitionException();
        var brief = await store.FindPlanningReadyBriefAsync(
            envelope.TenantId, proposal.BriefId, cancellationToken)
            ?? throw new ProposalStaleException();
        await EnsureAssetScopeAsync(
            envelope, envelope.Command.AgencyBrandAssetId, null, cancellationToken);
        await EnsureAssetScopeAsync(
            envelope, envelope.Command.ClientBrandAssetId, brief.ClientAccountId, cancellationToken);
        var primary = Colour(envelope.Command.PrimaryColour);
        var secondary = Colour(envelope.Command.SecondaryColour);
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET agency_brand_asset_id = {envelope.Command.AgencyBrandAssetId},
                client_brand_asset_id = {envelope.Command.ClientBrandAssetId},
                branding_primary_colour = {primary}, branding_secondary_colour = {secondary},
                unbranded_approved_by = NULL, unbranded_approved_at_utc = NULL,
                unbranded_approval_reason = NULL, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with
        {
            AgencyBrandAssetId = envelope.Command.AgencyBrandAssetId,
            ClientBrandAssetId = envelope.Command.ClientBrandAssetId,
            BrandingPrimaryColour = primary,
            BrandingSecondaryColour = secondary,
            UnbrandedApprovedBy = null,
            UnbrandedApprovedAtUtc = null,
            UnbrandedApprovalReason = null,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalUpdated,
            MasterDataReferences.CommercialEventTypes.ProposalUpdated, now);
    }

    private async Task<CommandOutcome> ApproveUnbrandedOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<ApproveUnbrandedProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(proposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Draft)
            throw new InvalidLifecycleTransitionException();
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET unbranded_approved_by = {envelope.ActorId.Value},
                unbranded_approved_at_utc = {now}, unbranded_approval_reason = {reason},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with
        {
            UnbrandedApprovedBy = envelope.ActorId.Value,
            UnbrandedApprovedAtUtc = now,
            UnbrandedApprovalReason = reason,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalUpdated,
            MasterDataReferences.CommercialEventTypes.ProposalUpdated, now);
    }

    private async Task EnsureAssetScopeAsync(
        CommandEnvelope<ConfigureProposalBrandingCommand> envelope,
        Guid? assetId,
        Guid? clientAccountId,
        CancellationToken cancellationToken)
    {
        if (!assetId.HasValue) return;
        var asset = await store.FindBrandAssetAsync(
            envelope.TenantId, assetId.Value, cancellationToken)
            ?? throw new ArgumentException("The selected brand asset is unavailable.");
        if (asset.ClientAccountId != clientAccountId)
            throw new ArgumentException("The selected brand asset belongs to a different brand.");
    }

    private static string? Colour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var colour = value.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(colour, "^#[0-9A-F]{6}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Brand colours must use six-digit hexadecimal notation.");
        return colour;
    }

    private static void EnsureJpeg(ProposalBrandDocument document)
    {
        if (!string.Equals(document.MediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            document.Content.Length is < 4 or > MaximumBrandAssetBytes ||
            document.Content[0] != 0xFF || document.Content[1] != 0xD8 ||
            document.Content[^2] != 0xFF || document.Content[^1] != 0xD9)
        {
            throw new ArgumentException("Brand assets must be valid JPEG files up to 2 MB.");
        }
    }

    private async Task EnsureClientBrandingReadyAsync(
        ProposalRow proposal,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (proposal.UnbrandedApprovedAtUtc is not null) return;
        if (!proposal.AgencyBrandAssetId.HasValue || !proposal.ClientBrandAssetId.HasValue)
            throw new ProposalBrandingRequiredException();
        var agency = await store.FindBrandAssetAsync(
            tenantId, proposal.AgencyBrandAssetId.Value, cancellationToken);
        var client = await store.FindBrandAssetAsync(
            tenantId, proposal.ClientBrandAssetId.Value, cancellationToken);
        if (agency?.ApprovedAtUtc is null || client?.ApprovedAtUtc is null)
            throw new ProposalBrandingRequiredException();
    }
}
