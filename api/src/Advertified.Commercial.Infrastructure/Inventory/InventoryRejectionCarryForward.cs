using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

// A changed mapping cannot turn previously rejected source records into accepted inventory.
internal static class InventoryRejectionCarryForward
{
    internal static async Task<IReadOnlySet<Guid>> FromHistoryAsync(GovernanceDbContext db, TenantId tenant,
        Guid importId, InventoryExtractionResult corrected, IReadOnlyList<PreparedInventoryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        // Include superseded decisions and tombstones: omitting a rejected row in one
        // mapping revision must not allow it to reappear in a later revision.
        var history = await db.Database.SqlQuery<InventoryCandidateRow>($"""
            SELECT id AS "Id", projection_id AS "ProjectionId", import_id AS "ImportId", row_number AS "RowNumber",
                {MasterDataCodes.LifecycleStatuses.Rejected} AS "Status", canonical_values_json::text AS "ValuesJson",
                validation_json::text AS "ValidationJson", source_locator AS "SourceLocator",
                reviewed_by AS "ReviewedBy", version AS "Version", updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.inventory_candidates
            WHERE tenant_id = {tenant.Value} AND import_id = {importId}
              AND (status_code = {MasterDataCodes.LifecycleStatuses.Rejected} OR soft_deleted_at_utc IS NOT NULL)
            """).ToListAsync(cancellationToken);
        var rejected = new HashSet<Guid>();
        foreach (var group in history.GroupBy(row => row.ProjectionId))
        {
            var artifact = await InventoryRetainedAcceptance.LoadAsync(db, tenant, group.First().Id,
                cancellationToken, includeHistory: true);
            rejected.UnionWith(Match(artifact.Extraction(), corrected, group.ToArray(), candidates));
        }
        return rejected;
    }

    internal static IReadOnlySet<Guid> Match(InventoryExtractionResult previous,
        InventoryExtractionResult corrected, IReadOnlyList<InventoryCandidateRow> current,
        IReadOnlyList<PreparedInventoryCandidate> candidates)
    {
        var rejected = current.Where(row => row.Status == MasterDataCodes.LifecycleStatuses.Rejected).ToArray();
        if (rejected.Length == 0) return new HashSet<Guid>();
        var metadata = previous.Document.DiscoveredSchema?.Records.SelectMany(record => record.FieldMappings
            .Concat(record.SupplierMetadataMappings).Concat(record.AssetMappings))
            .Where(mapping => mapping.IsDocumentMetadata)
            .Select(mapping => mapping.ValueSourceLocation ?? mapping.SourceLocation).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rejectedRow in rejected)
        {
            var raw = previous.Rows.SingleOrDefault(row => row.Number == rejectedRow.RowNumber &&
                row.Locator == rejectedRow.SourceLocator) ?? throw new InventoryPublishBlockedException();
            anchors.Add(raw.Locator);
            foreach (var field in raw.DiscoveredFields ?? [])
                if (!metadata.Contains(field.SourceLocator)) anchors.Add(field.SourceLocator);
        }
        var rejectedRows = corrected.Rows.Where(row => anchors.Contains(row.Locator) ||
            (row.DiscoveredFields ?? []).Any(field => anchors.Contains(field.SourceLocator)))
            .Select(row => row.Number).ToHashSet();
        return candidates.Where(candidate => rejectedRows.Contains(candidate.RowNumber))
            .Select(candidate => candidate.Id).ToHashSet();
    }
}
