using Npgsql;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly Guid InventorySupplierId =
        Guid.Parse("95000000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId =
        Guid.Parse("95000000-0000-0000-0000-000000000002");
    private static readonly Guid CandidateId =
        Guid.Parse("95000000-0000-0000-0000-000000000003");
    private static readonly Guid ProductVersionId =
        Guid.Parse("95000000-0000-0000-0000-000000000004");
    private static readonly Guid RateId =
        Guid.Parse("95000000-0000-0000-0000-000000000005");
    private static readonly Guid AvailabilityId =
        Guid.Parse("95000000-0000-0000-0000-000000000006");
    private static readonly Guid HistoricalRateId =
        Guid.Parse("95000000-0000-0000-0000-000000000007");
    private static readonly Guid HistoricalAvailabilityId =
        Guid.Parse("95000000-0000-0000-0000-000000000008");
    private static readonly Guid FutureRateId =
        Guid.Parse("95000000-0000-0000-0000-000000000009");
    private static readonly Guid FutureAvailabilityId =
        Guid.Parse("95000000-0000-0000-0000-000000000010");

    private static async Task SeedInventoryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        Add(batch, """
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, 'Verified Outdoor Media', 1, $3, $3)
            """, InventorySupplierId, SupplierTenantId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code, status_code,
                scan_status_code, quarantine_object_key, protected_object_key,
                source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'private-rate-card.csv', 'text/csv',
                'documentClasses', 'CSV', 'COMPLETED', 'CLEAN', 'private/quarantine',
                'private/protected-rate-card', repeat('a', 64), 100, $4, 2, $5, $5)
            """, ImportId, SupplierTenantId, InventorySupplierId, SupplierUserId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code, proposed_values_json,
                canonical_values_json, validation_json, source_locator, reviewed_by,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 1, 'APPROVED', '{}', '{}', '[]',
                'private-rate-card.csv#row=2', $4, 1, $5, $5)
            """, CandidateId, SupplierTenantId, ImportId, SupplierUserId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.inventory_products (
                id, tenant_id, supplier_id, supplier_product_code, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'JHB-N1-001', 'ACTIVE', 1, $4, $4)
            """, ProductId, SupplierTenantId, InventorySupplierId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code,
                product_type_code, geography, address, latitude, longitude,
                verification_code, source_import_id, source_candidate_id,
                published_by, published_at_utc)
            VALUES ($1, $2, $3, 1, 'N1 Highway Digital Billboard', 'OOH',
                'OOH_SITE', 'Johannesburg', 'Private supplier address', -26.100000,
                28.100000, 'HUMAN_VERIFIED', $4, $5, $6, $7)
            """, ProductVersionId, SupplierTenantId, ProductId, ImportId,
            CandidateId, SupplierUserId, InitialTime);
        Add(batch,
            "UPDATE commercial.inventory_products SET current_version_id = $1 WHERE id = $2",
            ProductVersionId, ProductId);
        Add(batch, """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', 900000,
                '2025-01-01', '2025-12-31', 'historical-rate-card.csv#row=2')
            """, HistoricalRateId, SupplierTenantId, ProductVersionId);
        Add(batch, """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', 1250000,
                '2026-01-01', '2027-12-31', 'private-rate-card.csv#row=2')
            """, RateId, SupplierTenantId, ProductVersionId);
        Add(batch, """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', 9900000,
                '2028-01-01', '2028-12-31', 'scheduled-rate-card.csv#row=2')
            """, FutureRateId, SupplierTenantId, ProductVersionId);
        Add(batch, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, valid_until_utc, source_locator)
            VALUES ($1, $2, $3, 'UNAVAILABLE', $4, $5, 'historical-confirmation')
            """, HistoricalAvailabilityId, SupplierTenantId, ProductVersionId,
            InitialTime.AddDays(-30), InitialTime.AddDays(-1));
        Add(batch, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, valid_until_utc, source_locator)
            VALUES ($1, $2, $3, 'AVAILABLE', $4, $5, 'private-confirmation')
            """, AvailabilityId, SupplierTenantId, ProductVersionId,
            InitialTime, InitialTime.AddYears(1));
        Add(batch, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, valid_until_utc, source_locator)
            VALUES ($1, $2, $3, 'UNAVAILABLE', $4, $5, 'scheduled-confirmation')
            """, FutureAvailabilityId, SupplierTenantId, ProductVersionId,
            InitialTime.AddYears(1), InitialTime.AddYears(2));
        await batch.ExecuteNonQueryAsync();
    }

    private static void Add(NpgsqlBatch batch, string sql, params object[] parameters)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in parameters) command.Parameters.AddWithValue(value);
        batch.BatchCommands.Add(command);
    }
}
