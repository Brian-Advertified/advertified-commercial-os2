using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> PublishOutcomeAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.CreatedBy == envelope.ActorId.Value)
        {
            throw new ApprovalRequiredException();
        }
        if (source.Status != Gate6InventoryStatuses.ReviewRequired ||
            source.ProtectedObjectKey is null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var candidates = await store.ListCandidatesAsync(
            envelope.TenantId, importId, cancellationToken);
        EnsurePublishable(candidates);
        var now = timeProvider.GetUtcNow();
        foreach (var candidate in candidates.Where(
            item => item.Status == Gate6InventoryStatuses.Approved))
        {
            await PublishCandidateAsync(
                envelope, source, candidate, now, cancellationToken);
        }
        await CompletePublicationAsync(
            envelope, source, now, cancellationToken);
        var updated = await store.FindImportAsync(
            envelope.TenantId, importId, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, importId, updated.Version,
            CommercialResourceTypes.InventoryImport, CommercialActions.InventoryPublished,
            CommercialEventTypes.InventoryPublished, now);
    }

    private static void EnsurePublishable(List<InventoryCandidateRow> candidates)
    {
        if (candidates.Count == 0 || candidates.Any(
                item => item.Status == Gate6InventoryStatuses.ReviewRequired) ||
            candidates.All(item => item.Status != Gate6InventoryStatuses.Approved))
        {
            throw new InventoryPublishBlockedException();
        }
        foreach (var candidate in candidates.Where(
            item => item.Status == Gate6InventoryStatuses.Approved))
        {
            var validation = JsonSerializer.Deserialize<InventoryValidationIssueView[]>(
                candidate.ValidationJson, InventoryRowMapper.StoredJson) ?? [];
            if (validation.Any(issue => issue.IsBlocking))
            {
                throw new InventoryPublishBlockedException();
            }
        }
    }

    private async Task PublishCandidateAsync(
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryImportRow source,
        InventoryCandidateRow candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var values = JsonSerializer.Deserialize<InventoryCandidateValues>(
            candidate.ValuesJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored inventory values are invalid.");
        var productCode = values.ProductCode
            ?? throw new InventoryPublishBlockedException();
        var product = await FindProductAsync(
            envelope.TenantId, source.SupplierId, productCode, cancellationToken);
        var productId = product?.Id ?? Guid.NewGuid();
        if (product is null)
        {
            await InsertProductAsync(
                envelope.TenantId, source.SupplierId, productId, productCode, now,
                cancellationToken);
        }
        var versionNumber = await NextVersionNumberAsync(
            envelope.TenantId, productId, cancellationToken);
        var versionId = Guid.NewGuid();
        await InsertProductVersionAsync(
            envelope, source, candidate, values, productId, versionId, versionNumber,
            now, cancellationToken);
        await InsertProductFactsAsync(
            envelope.TenantId, source, candidate, values, versionId, now, cancellationToken);
        await SetCurrentVersionAsync(
            envelope.TenantId, productId, versionId, product is not null, now,
            cancellationToken);
    }

    private Task<InventoryProductIdentityRow?> FindProductAsync(
        TenantId tenantId,
        Guid supplierId,
        string productCode,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryProductIdentityRow>($"""
            SELECT id AS "Id", version AS "Version"
            FROM commercial.inventory_products
            WHERE tenant_id = {tenantId.Value} AND supplier_id = {supplierId}
              AND supplier_product_code = {productCode}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<int> InsertProductAsync(
        TenantId tenantId, Guid supplierId, Guid productId, string productCode,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_products (
                id, tenant_id, supplier_id, supplier_product_code, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ({productId}, {tenantId.Value}, {supplierId}, {productCode},
                {Gate6InventoryStatuses.Active}, 1, {now}, {now})
            """, cancellationToken);

    private Task<int> NextVersionNumberAsync(
        TenantId tenantId, Guid productId, CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<int>($"""
            SELECT (COALESCE(MAX(version_number), 0) + 1)::integer AS "Value"
            FROM commercial.inventory_product_versions
            WHERE tenant_id = {tenantId.Value} AND product_id = {productId}
            """).SingleAsync(cancellationToken);

    private Task<int> InsertProductVersionAsync(
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryImportRow source,
        InventoryCandidateRow candidate,
        InventoryCandidateValues values,
        Guid productId,
        Guid versionId,
        int versionNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var extension = JsonSerializer.Serialize(
            values.Extension ?? new Dictionary<string, string>(), InventoryRowMapper.StoredJson);
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code,
                product_type_code, geography, address, latitude, longitude, extension_json,
                verification_code, source_import_id, source_candidate_id,
                published_by, published_at_utc)
            VALUES ({versionId}, {envelope.TenantId.Value}, {productId}, {versionNumber},
                {values.Name}, {values.Channel}, {values.ProductType}, {values.Geography},
                {values.Address}, {values.Latitude}, {values.Longitude}, {extension}::jsonb,
                {Gate6Verification.HumanVerified}, {source.Id}, {candidate.Id},
                {envelope.ActorId.Value}, {now})
            """, cancellationToken);
    }

    private async Task InsertProductFactsAsync(
        TenantId tenantId,
        InventoryImportRow source,
        InventoryCandidateRow candidate,
        InventoryCandidateValues values,
        Guid versionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, source_locator)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {versionId}, {values.RateType},
                {values.Currency}, {values.RateAmountMinor}, {candidate.SourceLocator});
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, source_locator)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {versionId}, {values.Availability},
                {now}, {candidate.SourceLocator});
            INSERT INTO commercial.inventory_assets (
                id, tenant_id, product_version_id, asset_type_code, object_key,
                content_hash, media_type, source_import_id)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {versionId},
                {AssetType(source, values)}, {source.ProtectedObjectKey}, {source.SourceHash},
                {source.DeclaredMediaType}, {source.Id})
            """, cancellationToken);
    }

    private static string AssetType(
        InventoryImportRow source,
        InventoryCandidateValues values) => source.DocumentClass switch
    {
        Gate6DocumentClasses.Png or Gate6DocumentClasses.Jpeg when values.Channel == "OOH" =>
            "OOH_PHOTO",
        Gate6DocumentClasses.Png or Gate6DocumentClasses.Jpeg => "PRODUCT_IMAGE",
        _ => "RATE_CARD",
    };

    private Task<int> SetCurrentVersionAsync(
        TenantId tenantId, Guid productId, Guid versionId, bool advanceVersion,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET current_version_id = {versionId},
                version = version + {(advanceVersion ? 1 : 0)}, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {productId}
            """, cancellationToken);

    private async Task CompletePublicationAsync(
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {Gate6InventoryStatuses.Completed}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {source.Id}
              AND status_code = {Gate6InventoryStatuses.ReviewRequired}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await RecordStepAsync(envelope.TenantId, source.Id, Gate6InventorySteps.Publication,
            Gate6InventoryStatuses.Completed, now, cancellationToken);
    }
}
