using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609120011_OpportunityDuplicateRejection")]
public sealed class OpportunityDuplicateRejection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO governance.master_data_collections
            (code, registry_version, effective_from, updated_at_utc)
        VALUES ('opportunityRejectionReasons', '2.42.0', DATE '2026-09-12', now())
        ON CONFLICT (code) DO NOTHING;
        INSERT INTO governance.master_data_items (
            collection_code, code, display_label, is_active, sort_order, metadata_json,
            effective_from, effective_to, created_at_utc, updated_at_utc)
        VALUES ('opportunityRejectionReasons', 'DUPLICATE_OPPORTUNITY',
            'This opportunity already exists', true, 10, '{"httpStatus":409}'::jsonb,
            DATE '2026-09-12', NULL, now(), now())
        ON CONFLICT (collection_code, code) DO NOTHING;

        -- Historical duplicates are retained. The source reference can contain 2048
        -- Unicode characters, so avoid putting its full value into a B-tree key.
        CREATE INDEX ix_opportunities_source_identity
            ON commercial.opportunities (tenant_id, client_account_id, source_type_code);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS commercial.ix_opportunities_source_identity;
        -- Stable reference codes and their audit history are deliberately retained.
        """);
}
