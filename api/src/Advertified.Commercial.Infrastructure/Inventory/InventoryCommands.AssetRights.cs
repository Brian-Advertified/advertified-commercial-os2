using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ReviewAssetRightsOutcomeAsync(
        Guid assetId,
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        CancellationToken cancellationToken)
    {
        var decision = NormalizeAssetRights(envelope.Command);
        await EnsureAssetRightsAttestorAsync(
            envelope, decision.AttestorRole, cancellationToken);
        var currentVersion = await CurrentAssetRightsVersionAsync(
            assetId, envelope, cancellationToken);
        if (currentVersion == 0)
        {
            throw new UnauthorizedAccessException("Inventory asset access denied.");
        }
        if (currentVersion != envelope.ExpectedVersion) throw new VersionConflictException();

        var now = timeProvider.GetUtcNow();
        var nextVersion = currentVersion + 1;
        var scopesJson = JsonSerializer.Serialize(decision.ScopeCodes);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_asset_rights_reviews (
                id, tenant_id, asset_id, asset_version, rights_status_code,
                rights_basis, licensed_until, scope_codes, territory_code,
                effective_on, until_revoked, attestor_role_code,
                evidence_reference, evidence_hash, reviewed_by, reviewed_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {assetId}, {nextVersion},
                {decision.Status}, {decision.Basis}, {decision.LicensedUntil},
                {scopesJson}::jsonb, {decision.Territory}, {decision.EffectiveOn},
                {decision.UntilRevoked}, {decision.AttestorRole},
                {decision.EvidenceReference}, {decision.EvidenceHash},
                {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        if (decision.Status == MasterDataCodes.AssetRightsStatuses.Revoked ||
            decision.LicensedUntil.HasValue)
        {
            await CreateAssetRightsTaskAsync(
                assetId, envelope, nextVersion, decision.LicensedUntil,
                now, cancellationToken);
        }
        var view = new InventoryAssetRightsReviewView(
            assetId, decision.Status, decision.Basis, decision.LicensedUntil,
            envelope.ActorId.Value, now, nextVersion, decision.ScopeCodes,
            decision.Territory, decision.EffectiveOn, decision.UntilRevoked,
            decision.AttestorRole, decision.EvidenceReference, decision.EvidenceHash);
        return OpportunityCommandSupport.Outcome(
            envelope, view, assetId, nextVersion,
            MasterDataReferences.CommercialResourceTypes.InventoryAsset,
            MasterDataReferences.CommercialActions.InventoryAssetRightsReviewed,
            MasterDataReferences.CommercialEventTypes.InventoryAssetRightsReviewed, now);
    }

    private Task<long> CurrentAssetRightsVersionAsync(
        Guid assetId,
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<long>($"""
            SELECT COALESCE(MAX(review.asset_version), 1) AS "Value"
            FROM commercial.inventory_assets asset
            LEFT JOIN commercial.inventory_asset_rights_reviews review
              ON review.tenant_id = asset.tenant_id AND review.asset_id = asset.id
            WHERE asset.tenant_id = {envelope.TenantId.Value} AND asset.id = {assetId}
            HAVING COUNT(asset.id) > 0
            """).SingleOrDefaultAsync(cancellationToken);

    private async Task EnsureAssetRightsAttestorAsync(
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        string attestorRole,
        CancellationToken cancellationToken)
    {
        var allowed = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.memberships membership
                WHERE membership.tenant_id = {envelope.TenantId.Value}
                  AND membership.user_id = {envelope.ActorId.Value}
                  AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                  AND membership.role_code = {attestorRole}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("Asset rights attestor denied.");
    }

    private Task<int> CreateAssetRightsTaskAsync(
        Guid assetId,
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        long version,
        DateOnly? licensedUntil,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dueAt = licensedUntil.HasValue
            ? new DateTimeOffset(
                licensedUntil.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, due_at_utc, version, created_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, NULL,
                {MasterDataCodes.HumanTaskTypes.AssetRightsRevalidation},
                {MasterDataCodes.LifecycleStatuses.Pending},
                {"Revalidate inventory asset rights"},
                {"Expired or revoked rights remove the asset from new proposals and public use."},
                {MasterDataReferences.CommercialResourceTypes.InventoryAsset.Value},
                {assetId}, {version}, {envelope.ActorId.Value}, {"{}"}::jsonb,
                {dueAt}, 1, {now})
            """, cancellationToken);
    }

    private static NormalizedAssetRights NormalizeAssetRights(
        ReviewInventoryAssetRightsCommand command)
    {
        var status = command.RightsStatus?.Trim().ToUpperInvariant();
        if (status is not (MasterDataCodes.AssetRightsStatuses.Approved or
            MasterDataCodes.AssetRightsStatuses.Unknown or
            MasterDataCodes.AssetRightsStatuses.Restricted or
            MasterDataCodes.AssetRightsStatuses.Revoked))
        {
            throw new ArgumentException("Select a supported asset rights status.");
        }
        var scopes = (command.ScopeCodes ?? []).Select(item => item.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).ToArray();
        if (scopes.Any(item => !AssetRightsScopes.Contains(item)))
        {
            throw new ArgumentException("Select supported asset rights scopes.");
        }
        var basis = OpportunityCommandSupport.Optional(
            command.RightsBasis, 1_000, nameof(command.RightsBasis));
        var territory = OpportunityCommandSupport.Required(
            command.TerritoryCode, 2, nameof(command.TerritoryCode)).ToUpperInvariant();
        var role = OpportunityCommandSupport.Required(
            command.AttestorRole ?? string.Empty, 100, nameof(command.AttestorRole));
        var reference = OpportunityCommandSupport.Optional(
            command.EvidenceReference, 1_000, nameof(command.EvidenceReference));
        var hash = command.EvidenceHash?.Trim().ToLowerInvariant();
        var material = status != MasterDataCodes.AssetRightsStatuses.Unknown;
        var approvedDates = command.EffectiveOn.HasValue &&
            (command.UntilRevoked ^ command.LicensedUntil.HasValue) &&
            (!command.LicensedUntil.HasValue ||
                command.LicensedUntil.Value >= command.EffectiveOn.Value);
        if (territory.Length != 2 || role is not (MasterDataCodes.Roles.PlatformAdmin or
                MasterDataCodes.Roles.SupplierAdmin) ||
            material && (basis is null || reference is null || !ValidHash(hash)) ||
            status == MasterDataCodes.AssetRightsStatuses.Approved &&
                (scopes.Length == 0 || !approvedDates))
        {
            throw new ArgumentException("The asset rights evidence or validity is incomplete.");
        }
        return new(
            status, basis, command.LicensedUntil, scopes, territory,
            command.EffectiveOn, command.UntilRevoked, role, reference, hash);
    }

    private static bool ValidHash(string? value) => value?.Length == 64 &&
        value.All(character => Uri.IsHexDigit(character));

    private static readonly HashSet<string> AssetRightsScopes = new(StringComparer.Ordinal)
    {
        MasterDataCodes.AssetRightsScopes.InternalPlanning,
        MasterDataCodes.AssetRightsScopes.NamedClientProposal,
        MasterDataCodes.AssetRightsScopes.MarketplaceDisplay,
        MasterDataCodes.AssetRightsScopes.PublicMarketingSocial,
    };

    private sealed record NormalizedAssetRights(
        string Status,
        string? Basis,
        DateOnly? LicensedUntil,
        string[] ScopeCodes,
        string Territory,
        DateOnly? EffectiveOn,
        bool UntilRevoked,
        string AttestorRole,
        string? EvidenceReference,
        string? EvidenceHash);
}
