using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609110007_AcceptedSupplierQuoteBookingLineage")]
public sealed class AcceptedSupplierQuoteBookingLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.bookings
            ADD COLUMN accepted_marketplace_response_id uuid,
            ADD COLUMN accepted_marketplace_response_version integer,
            ADD COLUMN accepted_marketplace_response_terms character varying(5000);

        ALTER TABLE commercial.bookings
            ADD CONSTRAINT ck_booking_accepted_response_snapshot CHECK (
                (accepted_marketplace_response_id IS NULL
                 AND accepted_marketplace_response_version IS NULL
                 AND accepted_marketplace_response_terms IS NULL)
                OR
                (accepted_marketplace_response_id IS NOT NULL
                 AND accepted_marketplace_response_version > 0
                 AND btrim(accepted_marketplace_response_terms) <> ''));

        ALTER TABLE commercial.bookings
            ADD CONSTRAINT fk_booking_accepted_marketplace_response
            FOREIGN KEY (buyer_tenant_id, supplier_tenant_id, accepted_marketplace_response_id)
            REFERENCES commercial.marketplace_supplier_responses(
                buyer_tenant_id, supplier_tenant_id, id);

        CREATE TRIGGER protect_booking_accepted_response_snapshot
            BEFORE UPDATE OF accepted_marketplace_response_id,
                accepted_marketplace_response_version,
                accepted_marketplace_response_terms
            ON commercial.bookings
            FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER protect_booking_accepted_response_snapshot ON commercial.bookings;
        ALTER TABLE commercial.bookings
            DROP CONSTRAINT fk_booking_accepted_marketplace_response,
            DROP CONSTRAINT ck_booking_accepted_response_snapshot,
            DROP COLUMN accepted_marketplace_response_terms,
            DROP COLUMN accepted_marketplace_response_version,
            DROP COLUMN accepted_marketplace_response_id;
        """);
}
