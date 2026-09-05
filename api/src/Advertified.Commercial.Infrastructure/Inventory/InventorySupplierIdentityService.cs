using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventorySupplierResolution(
    Guid? SupplierId,
    string SupplierName,
    string Status,
    string EvidenceJson,
    bool SupplierCreated);

internal sealed record ExtractedSupplierEvidence(
    string Name,
    string NormalizedName,
    string Basis,
    string Locator);

internal sealed record ExtractedSupplierChoice(
    string? Name,
    bool IsAmbiguous,
    string EvidenceJson);

public sealed class InventorySupplierIdentityService(
    InventoryRecordStore store,
    TimeProvider timeProvider)
{
    internal async Task<InventorySupplierResolution> ResolveHintAsync(
        TenantId tenantId,
        Guid? existingSupplierId,
        string? supplierNameHint,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        var hint = NormalizeDisplayName(supplierNameHint);
        if (existingSupplierId.HasValue)
        {
            var selected = await FindByIdAsync(
                tenantId, existingSupplierId.Value, cancellationToken)
                ?? throw new UnauthorizedAccessException("Supplier access denied.");
            if (hint is not null && !SameIdentity(selected.Name, hint))
            {
                throw new SupplierIdentityAmbiguousException();
            }
            return new InventorySupplierResolution(
                selected.Id, selected.Name,
                MasterDataCodes.InventorySupplierResolutionStatuses.Resolved,
                JsonSerializer.Serialize(new
                {
                    basis = "administrator_selected_supplier",
                    supplierId = selected.Id,
                    supplierName = selected.Name,
                }),
                false);
        }
        if (hint is null)
        {
            return PendingResolution();
        }
        var supplier = await FindOrCreateAsync(
            tenantId, hint, null, createdBy, cancellationToken);
        return new InventorySupplierResolution(
            supplier.Id, supplier.Name,
            MasterDataCodes.InventorySupplierResolutionStatuses.Resolved,
            JsonSerializer.Serialize(new
            {
                basis = "administrator_confirmed_hint",
                supplierName = hint,
            }),
            supplier.Created);
    }

    internal static InventorySupplierResolution ResolveExtraction(
        InventoryImportRow source,
        InventoryExtractionResult extraction)
    {
        // Authenticated/explicitly selected ownership is not a content inference.
        if (source.SupplierId.HasValue)
            return ExistingResolution(source, source.SupplierIdentityEvidenceJson);
        var choice = ChooseExtractedSupplier(source, extraction);
        // Content is a proposal for the administrator's explicit resolution
        // command, never authority to create or select a supplier implicitly.
        return choice.IsAmbiguous
            ? AmbiguousResolution(source, choice.EvidenceJson)
            : PendingResolution(choice.EvidenceJson);
    }

    internal async Task<InventorySupplierResolution> ResolveManualAsync(
        TenantId tenantId,
        Guid importId,
        Guid? existingSupplierId,
        string? supplierName,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken)
    {
        SupplierIdentityMatchRow supplier;
        var created = false;
        if (existingSupplierId.HasValue)
        {
            supplier = await FindByIdAsync(
                tenantId, existingSupplierId.Value, cancellationToken)
                ?? throw new UnauthorizedAccessException("Supplier access denied.");
        }
        else
        {
            var name = NormalizeDisplayName(supplierName)
                ?? throw new ArgumentException(
                    "A supplier name or existing supplier is required.",
                    nameof(supplierName));
            var result = await FindOrCreateAsync(
                tenantId, name, importId, actorId, cancellationToken);
            supplier = result.Match;
            created = result.Created;
        }
        return new InventorySupplierResolution(
            supplier.Id, supplier.Name,
            MasterDataCodes.InventorySupplierResolutionStatuses.Resolved,
            JsonSerializer.Serialize(new
            {
                basis = "administrator_resolution",
                supplierId = supplier.Id,
                supplierName = supplier.Name,
                reason,
                resolvedAtUtc = timeProvider.GetUtcNow(),
            }),
            created);
    }

    private static ExtractedSupplierChoice ChooseExtractedSupplier(
        InventoryImportRow source,
        InventoryExtractionResult extraction)
    {
        var evidence = ReadSupplierEvidence(extraction);
        var supplied = DistinctNames(evidence.Where(item =>
            item.Basis == MasterDataCodes.InventoryEvidenceBases.SupplierSupplied));
        var derived = DistinctNames(evidence.Where(item =>
            item.Basis != MasterDataCodes.InventoryEvidenceBases.SupplierSupplied));
        var selected = supplied.Count == 1
            ? supplied[0]
            : supplied.Count == 0 && derived.Count == 1
                ? derived[0]
                : null;
        var ambiguous = supplied.Count > 1 ||
            (supplied.Count == 0 && derived.Count > 1);
        var json = JsonSerializer.Serialize(new
        {
            hint = source.SupplierNameHint,
            selected,
            ambiguous,
            evidence,
        });
        return new ExtractedSupplierChoice(selected, ambiguous, json);
    }

    private static ExtractedSupplierEvidence[] ReadSupplierEvidence(
        InventoryExtractionResult extraction) =>
        extraction.Rows.Select(row => InventoryCandidateNormalizer.Normalize(
                row, extraction.SourceHash, DateTimeOffset.UnixEpoch))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SupplierName))
            .Select(candidate =>
            {
                var field = candidate.Evidence.FirstOrDefault(item =>
                    item.FieldName == "supplier_name");
                return new ExtractedSupplierEvidence(
                    candidate.SupplierName!,
                    NormalizeIdentity(candidate.SupplierName!),
                    field?.EvidenceBasis ??
                        MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
                    field?.SourceLocator ?? candidate.Locator);
            })
            .Where(item => item.NormalizedName.Length > 0)
            .DistinctBy(item => new
            {
                item.NormalizedName,
                item.Basis,
                item.Locator,
            })
            .ToArray();

    private static List<string> DistinctNames(
        IEnumerable<ExtractedSupplierEvidence> evidence) =>
        evidence.GroupBy(item => item.NormalizedName, StringComparer.Ordinal)
            .Select(group => group.First().Name)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<(SupplierIdentityMatchRow Match, Guid Id, string Name, bool Created)>
        FindOrCreateAsync(
            TenantId tenantId,
            string supplierName,
            Guid? sourceImportId,
            Guid createdBy,
            CancellationToken cancellationToken)
    {
        var matches = await FindMatchesAsync(
            tenantId, supplierName, cancellationToken);
        if (matches.Count > 1)
        {
            throw new SupplierIdentityAmbiguousException();
        }
        if (matches.Count == 1)
        {
            var match = matches[0];
            return (match, match.Id, match.Name, false);
        }
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, claim_status_code,
                created_from_import_id, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {tenantId.Value}, {supplierName},
                {MasterDataCodes.SupplierClaimStatuses.Unclaimed},
                {sourceImportId}, 1, {now}, {now})
            ON CONFLICT (tenant_id, (lower(name))) DO NOTHING
            """, cancellationToken);
        var persisted = await FindMatchesAsync(
            tenantId, supplierName, cancellationToken);
        if (persisted.Count != 1)
        {
            throw new SupplierIdentityAmbiguousException();
        }
        var result = persisted[0];
        return (result, result.Id, result.Name, result.Id == id);
    }

    private Task<List<SupplierIdentityMatchRow>> FindMatchesAsync(
        TenantId tenantId,
        string supplierName,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<SupplierIdentityMatchRow>($"""
            SELECT id AS "Id", name AS "Name",
                claim_status_code AS "ClaimStatus", version AS "Version"
            FROM commercial.inventory_suppliers
            WHERE tenant_id = {tenantId.Value}
              AND (lower(name) = lower({supplierName})
                OR identity_key = regexp_replace(
                    lower({supplierName}), '[^a-z0-9]+', '', 'g'))
            ORDER BY CASE WHEN lower(name) = lower({supplierName}) THEN 0 ELSE 1 END,
                id
            """).ToListAsync(cancellationToken);

    private Task<SupplierIdentityMatchRow?> FindByIdAsync(
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<SupplierIdentityMatchRow>($"""
            SELECT id AS "Id", name AS "Name",
                claim_status_code AS "ClaimStatus", version AS "Version"
            FROM commercial.inventory_suppliers
            WHERE tenant_id = {tenantId.Value} AND id = {supplierId}
            """).SingleOrDefaultAsync(cancellationToken);

    private static InventorySupplierResolution PendingResolution(
        string evidenceJson = "{}") => new(
            null, "Supplier to be identified",
            MasterDataCodes.InventorySupplierResolutionStatuses.Pending,
            evidenceJson, false);

    private static InventorySupplierResolution ExistingResolution(
        InventoryImportRow source,
        string evidenceJson) => new(
            source.SupplierId, source.SupplierName,
            MasterDataCodes.InventorySupplierResolutionStatuses.Resolved,
            evidenceJson, false);

    private static InventorySupplierResolution AmbiguousResolution(
        InventoryImportRow source,
        string evidenceJson) => new(
            source.SupplierId, source.SupplierName,
            MasterDataCodes.InventorySupplierResolutionStatuses.Ambiguous,
            evidenceJson, false);

    private static string? NormalizeDisplayName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : OpportunityCommandSupport.Required(value, 300, nameof(value));

    private static bool SameIdentity(string left, string right) =>
        string.Equals(
            NormalizeIdentity(left), NormalizeIdentity(right),
            StringComparison.Ordinal);

    private static string NormalizeIdentity(string value) =>
        new(value.Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
