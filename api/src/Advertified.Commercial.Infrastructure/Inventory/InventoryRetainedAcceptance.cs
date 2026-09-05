using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Server-owned artifact lookup is the authority. Editable Extension metadata is audit display only.
internal static class InventoryRetainedAcceptance
{
    internal static async Task<InventoryAcceptanceArtifact?> LoadImportAsync(GovernanceDbContext db,
        TenantId tenant, Guid importId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<InventoryAcceptanceArtifact>($"""
            SELECT projection.id AS "ProjectionId", extraction.id AS "ExtractionId",
                extraction.source_hash AS "SourceHash",
                COALESCE(extraction.source_file_version, 0) AS "SourceFileVersion",
                projection.projector_code AS "AdapterCode", projection.projector_version AS "AdapterVersion",
                projection.schema_version AS "SchemaVersion", extraction.provider_json::text AS "ProviderJson",
                COALESCE(projection.canonical_json, extraction.canonical_json)::text AS "CanonicalJson",
                projection.canonical_output_hash AS "CanonicalHash"
            FROM commercial.inventory_extraction_projections projection
            JOIN commercial.inventory_extractions extraction
                ON extraction.tenant_id = projection.tenant_id AND extraction.id = projection.input_artifact_id
            WHERE projection.tenant_id = {tenant.Value} AND projection.import_id = {importId}
            ORDER BY projection.created_at_utc DESC, projection.id DESC LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    internal static async Task<InventoryAcceptanceArtifact> LoadAsync(GovernanceDbContext db,
        TenantId tenant, Guid candidateId, CancellationToken cancellationToken, bool includeHistory = false) =>
        await db.Database.SqlQuery<InventoryAcceptanceArtifact>($"""
            SELECT projection.id AS "ProjectionId", extraction.id AS "ExtractionId",
                extraction.source_hash AS "SourceHash",
                COALESCE(extraction.source_file_version, 0) AS "SourceFileVersion",
                projection.projector_code AS "AdapterCode", projection.projector_version AS "AdapterVersion",
                projection.schema_version AS "SchemaVersion",
                extraction.provider_json::text AS "ProviderJson",
                COALESCE(projection.canonical_json, extraction.canonical_json)::text AS "CanonicalJson",
                projection.canonical_output_hash AS "CanonicalHash"
            FROM commercial.inventory_candidates candidate
            JOIN commercial.inventory_extraction_projections projection
                ON projection.tenant_id = candidate.tenant_id AND projection.id = candidate.projection_id
            JOIN commercial.inventory_extractions extraction
                ON extraction.tenant_id = projection.tenant_id AND extraction.id = projection.input_artifact_id
            WHERE candidate.tenant_id = {tenant.Value} AND candidate.id = {candidateId}
              AND ({includeHistory} OR candidate.superseded_at_utc IS NULL)
            """).SingleAsync(cancellationToken);

    internal static PreparedInventoryCandidate[] Evaluate(InventoryAcceptanceArtifact artifact,
        InventoryImportRow source, InventoryCodeSets codes, DateTimeOffset now)
    {
        var extraction = artifact.Extraction();
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(extraction.Rows, artifact.SourceHash,
            source.SupplierName, codes, now);
        return InventoryAcceptancePolicy.Apply(extraction, source.SourceHash,
            artifact.SourceFileVersion, codes, candidates, now);
    }

    internal static void EnsureMatches(InventoryCandidateRow row, PreparedInventoryCandidate[] reevaluated)
    {
        var candidate = reevaluated.SingleOrDefault(item => item.RowNumber == row.RowNumber &&
            item.SourceLocator == row.SourceLocator) ?? throw new InventoryPublishBlockedException();
        var values = JsonSerializer.Deserialize<InventoryCandidateValues>(row.ValuesJson, InventoryRowMapper.StoredJson)
            ?? throw new InventoryPublishBlockedException();
        if (InventoryCandidateReviewPolicy.RequiresReview(candidate) ||
            InventoryAcceptancePolicy.CandidateRevision(values) != InventoryAcceptancePolicy.CandidateRevision(candidate.Values))
            throw new InventoryPublishBlockedException();
    }
}

internal sealed class InventoryAcceptanceArtifact
{
    public Guid ProjectionId { get; set; }
    public Guid ExtractionId { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public long SourceFileVersion { get; set; }
    public string AdapterCode { get; set; } = string.Empty;
    public string AdapterVersion { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string ProviderJson { get; set; } = string.Empty;
    public string CanonicalJson { get; set; } = string.Empty;
    public string CanonicalHash { get; set; } = string.Empty;

    internal InventoryExtractionResult Extraction()
    {
        var document = InventoryExtractionContract.Replay(CanonicalJson, SchemaVersion);
        var extraction = InventoryExtractionContract.Create(AdapterCode, AdapterVersion, SchemaVersion,
            SourceHash, ProviderJson, document.Rows, document.DiscoveredSchema, document.SchemaDiscoveryFailure);
        if (extraction.CanonicalOutputHash != CanonicalHash) throw new InventoryExtractionUnavailableException();
        return extraction;
    }
}
