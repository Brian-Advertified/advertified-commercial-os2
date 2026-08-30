using System.Text.Json;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeRecordStore
{
    internal async Task<Guid> CreateAssetShellAsync<TCommand>(
        CreativeRequirementSourceRow requirement,
        CommandEnvelope<TCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var id = Guid.NewGuid();
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.creative_assets (
                id, buyer_tenant_id, supplier_tenant_id, campaign_id, requirement_id,
                current_version_id, version, created_by, created_at_utc, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {requirement.SupplierTenantId},
                {requirement.CampaignId}, {requirement.Id}, NULL, 0,
                {envelope.ActorId.Value}, {now}, {now})
            ON CONFLICT (buyer_tenant_id, requirement_id) DO NOTHING
            """, cancellationToken);
        if (changed != 1) throw new CreativeReadinessBlockedException();
        return id;
    }

    internal async Task<Guid> InsertVersionAsync<TCommand>(
        CreativeAssetStateRow asset,
        CreativeRequirementSourceRow requirement,
        CommandEnvelope<TCommand> envelope,
        PreparedCreativeFile file,
        string approvedCopy,
        string objectKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var id = Guid.NewGuid();
        var nextVersion = checked((asset.CurrentVersionNumber ?? 0) + 1);
        var commercial = JsonSerializer.Serialize(new
        {
            currency = requirement.Currency,
            supplierCostMinor = requirement.SupplierCostMinor,
            clientPriceMinor = requirement.ClientPriceMinor,
            feesMinor = requirement.FeesMinor,
            vatMinor = requirement.VatMinor,
        });
        var inserted = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.creative_asset_versions (
                id, buyer_tenant_id, supplier_tenant_id, asset_id, requirement_id,
                version_number, asset_type_code, file_name, media_type, size_bytes,
                content_sha256, protected_object_key, approved_copy,
                commercial_snapshot_json, campaign_version, booking_version,
                created_by, created_at_utc)
            VALUES ({id}, {asset.BuyerTenantId}, {asset.SupplierTenantId}, {asset.Id},
                {requirement.Id}, {nextVersion}, {MasterDataCodes.AssetTypes.CreativeFile},
                {file.FileName}, {file.MediaType}, {file.Content.LongLength}, {file.Sha256},
                {objectKey}, {approvedCopy}, CAST({commercial} AS jsonb),
                {requirement.CampaignVersion}, {requirement.BookingVersion},
                {envelope.ActorId.Value}, {now})
            ON CONFLICT (buyer_tenant_id, asset_id, version_number) DO NOTHING
            """, cancellationToken);
        if (inserted != 1) throw new CreativeReadinessBlockedException();
        var updated = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.creative_assets
            SET current_version_id = {id}, version = version + 1, updated_at_utc = {now}
            WHERE id = {asset.Id} AND buyer_tenant_id = {asset.BuyerTenantId}
              AND version = {asset.Version}
            """, cancellationToken);
        if (updated != 1) throw new CreativeReadinessBlockedException();
        return id;
    }

    internal async Task InsertReviewAsync<TCommand>(
        CreativeAssetStateRow asset,
        CommandEnvelope<TCommand> envelope,
        string reviewType,
        string decision,
        string? rightsStatus,
        string evidenceReference,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        if (asset.CurrentVersionId is null) throw new CreativeReadinessBlockedException();
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.creative_asset_reviews (
                id, buyer_tenant_id, supplier_tenant_id, asset_id, asset_version_id,
                review_type_code, decision_code, rights_status_code, evidence_reference,
                reason, reviewed_by, reviewer_tenant_id, reviewed_at_utc)
            VALUES ({Guid.NewGuid()}, {asset.BuyerTenantId}, {asset.SupplierTenantId},
                {asset.Id}, {asset.CurrentVersionId.Value}, {reviewType}, {decision},
                {rightsStatus}, {evidenceReference}, {reason}, {envelope.ActorId.Value},
                {envelope.TenantId.Value}, {now})
            ON CONFLICT (buyer_tenant_id, asset_version_id, review_type_code) DO NOTHING
            """, cancellationToken);
        if (changed != 1) throw new CreativeReadinessBlockedException();
        var advanced = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.creative_assets
            SET version = version + 1, updated_at_utc = {now}
            WHERE id = {asset.Id} AND version = {asset.Version}
            """, cancellationToken);
        if (advanced != 1) throw new CreativeReadinessBlockedException();
    }
}
