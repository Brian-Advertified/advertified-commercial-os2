using Npgsql;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task SeedPlanningPrerequisitesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, title, owner_user_id, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'Johannesburg launch', $4, 'APPROVED', 1, $5, $5)
            """, BriefId, TenantId, ClientId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title, content,
                content_hash, created_by, created_at_utc)
            VALUES ($1, $2, $3, 'SUPPLIED_TEXT', 'owner:supplied', 'Approved source',
                'Approved campaign source', repeat('a', 64), $4, $5)
            """, BriefSourceId, TenantId, BriefId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            VALUES ($1, $2, $3, $4, 1, 'Create qualified local demand',
                'Increase qualified enquiries', '["Local business decision makers"]',
                '["Johannesburg"]', 'September 2026', 1000000, false, 'ZAR',
                'REGISTERED', 5000, '[]', '[]', '["Owner supplied objective"]',
                '[]', '[]', '[]', '[]', 'APPROVED', $5, $5, $6, 1, $6)
            """, BriefVersionId, TenantId, BriefId, BriefSourceId, OperatorId, Now);
        AddCommand(batch,
            """
            UPDATE commercial.campaign_briefs
            SET current_draft_version_id = $1, approved_version_id = $1
            WHERE id = $2
            """, BriefVersionId, BriefId);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, 'Planning Media', 1, $3, $3)
            """, SupplierId, TenantId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code, status_code,
                scan_status_code, quarantine_object_key, protected_object_key,
                source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'planning.csv', 'text/csv', 'documentClasses', 'CSV',
                'COMPLETED', 'CLEAN', 'q/planning', 'p/planning', repeat('b', 64), 100,
                $4, 2, $5, $5)
            """, ImportId, TenantId, SupplierId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code, proposed_values_json,
                canonical_values_json, validation_json, source_locator, reviewed_by,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 1, 'APPROVED', '{}', '{}', '[]', 'csv#row=2',
                $4, 1, $5, $5)
            """, CandidateId, TenantId, ImportId, OperatorId, Now);
        await batch.ExecuteNonQueryAsync();

        var rates = new long[] { 100_000, 120_000, 140_000, 160_000, 180_000, 90_000 };
        for (var index = 0; index < rates.Length; index++)
        {
            await InsertProductAsync(connection, index, rates[index]);
        }
    }

    private static async Task InsertProductAsync(
        NpgsqlConnection connection,
        int index,
        long rate)
    {
        var productId = Guid.Parse($"77000000-0000-0000-0000-{index + 1:D12}");
        var versionId = Guid.Parse($"78000000-0000-0000-0000-{index + 1:D12}");
        var rateId = Guid.Parse($"79000000-0000-0000-0000-{index + 1:D12}");
        var availabilityId = Guid.Parse($"7a000000-0000-0000-0000-{index + 1:D12}");
        var geography = index == 5 ? "Cape Town" : "Johannesburg";
        var effectiveTo = index == 4
            ? new DateOnly(2026, 8, 31)
            : new DateOnly(2027, 1, 1);
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_products (
                id, tenant_id, supplier_id, supplier_product_code, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, $4, 'ACTIVE', 1, $5, $5)
            """, productId, TenantId, SupplierId, $"PLANNING-{index + 1}", Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code,
                product_type_code, geography, latitude, longitude, verification_code,
                source_import_id, source_candidate_id, published_by, published_at_utc)
            VALUES ($1, $2, $3, 1, $4, 'OOH', 'OOH_SITE', $5, $6, $7,
                'HUMAN_VERIFIED', $8, $9, $10, $11)
            """, versionId, TenantId, productId, $"{geography} Site {index + 1}", geography,
            index == 5 ? -33.9249m : -26.2041m,
            index == 5 ? 18.4241m : 28.0473m,
            ImportId, CandidateId, OperatorId, Now);
        AddCommand(batch,
            "UPDATE commercial.inventory_products SET current_version_id = $1 WHERE id = $2",
            versionId, productId);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', $4, '2026-01-01', $5,
                'csv#row=2')
            """, rateId, TenantId, versionId, rate, effectiveTo);
        var availability = index == 0 ? "AVAILABLE" : "UNKNOWN";
        var validUntil = index == 0
            ? new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero)
            : (DateTimeOffset?)null;
        var availabilitySource = index == 0
            ? "supplier-confirmation:email-001"
            : "supplier-rate-card";
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code, observed_at_utc,
                valid_until_utc, source_locator)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            """, availabilityId, TenantId, versionId, availability, Now,
            validUntil.HasValue ? validUntil.Value : DBNull.Value, availabilitySource);
        await batch.ExecuteNonQueryAsync();
    }

    private static void AddCommand(NpgsqlBatch batch, string sql, params object[] parameters)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in parameters)
        {
            command.Parameters.AddWithValue(value);
        }
        batch.BatchCommands.Add(command);
    }
}
