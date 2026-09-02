namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryDuplicateQueries
{
    internal const string Select = """
        SELECT candidate.id AS "Id", candidate.left_product_id AS "LeftProductId",
            candidate.right_product_id AS "RightProductId",
            candidate.left_product_version_id AS "LeftProductVersionId",
            candidate.right_product_version_id AS "RightProductVersionId",
            left_version.name AS "LeftName", right_version.name AS "RightName",
            candidate.method_code AS "Method", candidate.similarity AS "Similarity",
            candidate.evidence_json::text AS "EvidenceJson",
            candidate.status_code AS "Status",
            candidate.canonical_product_id AS "CanonicalProductId",
            candidate.reviewed_by AS "ReviewedBy",
            candidate.reviewed_at_utc AS "ReviewedAtUtc",
            candidate.review_reason AS "ReviewReason", candidate.version AS "Version"
        FROM commercial.inventory_duplicate_candidates candidate
        JOIN commercial.inventory_product_versions left_version
          ON left_version.tenant_id = candidate.tenant_id
         AND left_version.id = candidate.left_product_version_id
        JOIN commercial.inventory_product_versions right_version
          ON right_version.tenant_id = candidate.tenant_id
         AND right_version.id = candidate.right_product_version_id
        """;
}
