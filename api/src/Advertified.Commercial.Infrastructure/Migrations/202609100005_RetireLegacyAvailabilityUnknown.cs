using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609100005_RetireLegacyAvailabilityUnknown")]
public sealed class RetireLegacyAvailabilityUnknown : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO governance.master_data_items (
            collection_code, code, display_label, is_active, sort_order, metadata_json,
            effective_from, effective_to, created_at_utc, updated_at_utc)
        SELECT
            'availabilityStatuses', 'PLANNING_AVAILABLE',
            'Planning Available — Supplier Confirmation Required', true, 50,
            '{"planningEligible":true,"supplyConfidence":"UNCONFIRMED","bookingEligible":false}'::jsonb,
            DATE '2026-09-10', NULL, now(), now()
        WHERE EXISTS (
            SELECT 1 FROM governance.master_data_collections
            WHERE code = 'availabilityStatuses')
        ON CONFLICT (collection_code, code) DO UPDATE
        SET display_label = EXCLUDED.display_label,
            is_active = EXCLUDED.is_active,
            metadata_json = EXCLUDED.metadata_json,
            effective_from = EXCLUDED.effective_from,
            effective_to = NULL,
            updated_at_utc = now();

        ALTER TABLE commercial.inventory_availability
            DISABLE TRIGGER protect_inventory_availability;
        ALTER TABLE commercial.marketplace_listing_versions
            DISABLE TRIGGER protect_marketplace_listing_versions;
        ALTER TABLE commercial.marketplace_supplier_responses
            DISABLE TRIGGER protect_marketplace_supplier_responses;
        ALTER TABLE commercial.supply_coordination
            DISABLE TRIGGER protect_supply_coordination;

        UPDATE commercial.inventory_availability
        SET availability_code = 'PLANNING_AVAILABLE'
        WHERE availability_collection_code = 'availabilityStatuses'
          AND availability_code = 'UNKNOWN';

        UPDATE commercial.marketplace_listing_versions
        SET availability_code = 'PLANNING_AVAILABLE'
        WHERE availability_code = 'UNKNOWN';

        UPDATE commercial.marketplace_supplier_responses
        SET availability_code = 'PLANNING_AVAILABLE'
        WHERE availability_collection_code = 'availabilityStatuses'
          AND availability_code = 'UNKNOWN';

        UPDATE commercial.supply_coordination
        SET availability_code = 'PLANNING_AVAILABLE'
        WHERE availability_collection_code = 'availabilityStatuses'
          AND availability_code = 'UNKNOWN';

        ALTER TABLE commercial.inventory_availability
            ENABLE TRIGGER protect_inventory_availability;
        ALTER TABLE commercial.marketplace_listing_versions
            ENABLE TRIGGER protect_marketplace_listing_versions;
        ALTER TABLE commercial.marketplace_supplier_responses
            ENABLE TRIGGER protect_marketplace_supplier_responses;
        ALTER TABLE commercial.supply_coordination
            ENABLE TRIGGER protect_supply_coordination;

        ALTER TABLE governance.master_data_items
            DISABLE TRIGGER protect_master_data_item_delete;
        DELETE FROM governance.master_data_items
        WHERE collection_code = 'availabilityStatuses'
          AND code = 'UNKNOWN';
        ALTER TABLE governance.master_data_items
            ENABLE TRIGGER protect_master_data_item_delete;

        UPDATE governance.master_data_items
        SET sort_order = 10, updated_at_utc = now()
        WHERE collection_code = 'availabilityStatuses' AND code = 'AVAILABLE';
        UPDATE governance.master_data_items
        SET sort_order = 20, updated_at_utc = now()
        WHERE collection_code = 'availabilityStatuses' AND code = 'LIMITED';
        UPDATE governance.master_data_items
        SET sort_order = 30, updated_at_utc = now()
        WHERE collection_code = 'availabilityStatuses' AND code = 'UNAVAILABLE';
        UPDATE governance.master_data_items
        SET sort_order = 40, updated_at_utc = now()
        WHERE collection_code = 'availabilityStatuses' AND code = 'PLANNING_AVAILABLE';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Retiring legacy UNKNOWN availability is a forward-only canonical-data change.");
}
