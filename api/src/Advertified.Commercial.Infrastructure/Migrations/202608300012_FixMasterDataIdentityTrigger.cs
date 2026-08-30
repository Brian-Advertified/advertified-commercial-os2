using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300012_FixMasterDataIdentityTrigger")]
public sealed class FixMasterDataIdentityTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION governance.reject_master_data_identity_change()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_TABLE_NAME = 'master_data_collections' THEN
                    IF OLD.code IS DISTINCT FROM NEW.code THEN
                        RAISE EXCEPTION 'Master-data collection codes are immutable';
                    END IF;
                ELSIF TG_TABLE_NAME = 'master_data_items' THEN
                    IF OLD.collection_code IS DISTINCT FROM NEW.collection_code
                       OR OLD.code IS DISTINCT FROM NEW.code THEN
                        RAISE EXCEPTION 'Master-data item codes are immutable';
                    END IF;
                ELSE
                    RAISE EXCEPTION 'Unsupported master-data identity trigger table: %',
                        TG_TABLE_NAME;
                END IF;
                RETURN NEW;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION governance.reject_master_data_identity_change()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_TABLE_NAME = 'master_data_collections' AND OLD.code <> NEW.code THEN
                    RAISE EXCEPTION 'Master-data collection codes are immutable';
                END IF;
                IF TG_TABLE_NAME = 'master_data_items'
                   AND (OLD.collection_code <> NEW.collection_code OR OLD.code <> NEW.code) THEN
                    RAISE EXCEPTION 'Master-data item codes are immutable';
                END IF;
                RETURN NEW;
            END
            $$;
            """);
    }
}
