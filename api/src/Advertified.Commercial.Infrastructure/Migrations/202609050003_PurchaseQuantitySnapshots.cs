using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609050003_PurchaseQuantitySnapshots")]
public sealed class PurchaseQuantitySnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.media_plan_lines ADD COLUMN purchase_json jsonb
            CHECK (purchase_json IS NULL OR jsonb_typeof(purchase_json) = 'object');
        ALTER TABLE commercial.bookings ADD COLUMN purchase_json jsonb
            CHECK (purchase_json IS NULL OR jsonb_typeof(purchase_json) = 'object');
        CREATE TRIGGER protect_booking_purchase_snapshot BEFORE UPDATE OF purchase_json
            ON commercial.bookings FOR EACH ROW
            WHEN (OLD.purchase_json IS DISTINCT FROM NEW.purchase_json)
            EXECUTE FUNCTION commercial.reject_immutable_record_change();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM commercial.media_plan_lines WHERE purchase_json IS NOT NULL)
                OR EXISTS (SELECT 1 FROM commercial.bookings WHERE purchase_json IS NOT NULL) THEN
                RAISE EXCEPTION 'Retained purchase quantities must not be discarded';
            END IF;
        END $$;
        DROP TRIGGER protect_booking_purchase_snapshot ON commercial.bookings;
        ALTER TABLE commercial.bookings DROP COLUMN purchase_json;
        ALTER TABLE commercial.media_plan_lines DROP COLUMN purchase_json;
        """);
}
