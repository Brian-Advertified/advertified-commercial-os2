using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290001_GovernanceFoundation")]
public sealed class GovernanceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("governance");

        CreateCollections(migrationBuilder);
        CreateItems(migrationBuilder);
        CreateHistory(migrationBuilder);
        CreateProtectionTriggers(migrationBuilder);
        CreateAuditTrigger(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS governance CASCADE;");
    }

    private static void CreateCollections(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "master_data_collections",
            schema: "governance",
            columns: table => new
            {
                code = table.Column<string>(type: "character varying(100)", maxLength: 100),
                registry_version = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50),
                effective_from = table.Column<DateOnly>(type: "date"),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_master_data_collections", item => item.code);
            });
    }

    private static void CreateItems(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "master_data_items",
            schema: "governance",
            columns: table => new
            {
                collection_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100),
                display_label = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200),
                is_active = table.Column<bool>(type: "boolean"),
                sort_order = table.Column<int>(type: "integer"),
                metadata_json = table.Column<string>(type: "jsonb"),
                effective_from = table.Column<DateOnly>(type: "date"),
                effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone"),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone"),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_master_data_items",
                    item => new { item.collection_code, item.code });
                table.ForeignKey(
                    name: "fk_master_data_items_collections",
                    column: item => item.collection_code,
                    principalSchema: "governance",
                    principalTable: "master_data_collections",
                    principalColumn: "code",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_master_data_items_sort_order", "sort_order > 0");
                table.CheckConstraint(
                    "ck_master_data_items_effective_dates",
                    "effective_to IS NULL OR effective_to > effective_from");
            });

        migrationBuilder.CreateIndex(
            name: "ux_master_data_items_collection_sort",
            schema: "governance",
            table: "master_data_items",
            columns: ["collection_code", "sort_order"],
            unique: true);
    }

    private static void CreateHistory(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "master_data_item_history",
            schema: "governance",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint")
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                collection_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100),
                item_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100),
                operation = table.Column<string>(
                    type: "character varying(10)",
                    maxLength: 10),
                snapshot_json = table.Column<string>(type: "jsonb"),
                changed_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_master_data_item_history", item => item.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_master_data_history_item_time",
            schema: "governance",
            table: "master_data_item_history",
            columns: ["collection_code", "item_code", "changed_at_utc"]);
    }

    private static void CreateProtectionTriggers(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE FUNCTION governance.reject_master_data_identity_change()
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

            CREATE TRIGGER protect_master_data_collection_identity
            BEFORE UPDATE ON governance.master_data_collections
            FOR EACH ROW EXECUTE FUNCTION governance.reject_master_data_identity_change();

            CREATE TRIGGER protect_master_data_item_identity
            BEFORE UPDATE ON governance.master_data_items
            FOR EACH ROW EXECUTE FUNCTION governance.reject_master_data_identity_change();

            CREATE FUNCTION governance.reject_master_data_delete()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Master data must be deactivated, not deleted';
            END
            $$;

            CREATE TRIGGER protect_master_data_collection_delete
            BEFORE DELETE ON governance.master_data_collections
            FOR EACH ROW EXECUTE FUNCTION governance.reject_master_data_delete();

            CREATE TRIGGER protect_master_data_item_delete
            BEFORE DELETE ON governance.master_data_items
            FOR EACH ROW EXECUTE FUNCTION governance.reject_master_data_delete();
            """);
    }

    private static void CreateAuditTrigger(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE FUNCTION governance.audit_master_data_item()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO governance.master_data_item_history
                    (collection_code, item_code, operation, snapshot_json, changed_at_utc)
                VALUES
                    (NEW.collection_code, NEW.code, TG_OP, to_jsonb(NEW), CURRENT_TIMESTAMP);
                RETURN NEW;
            END
            $$;

            CREATE TRIGGER audit_master_data_item
            AFTER INSERT OR UPDATE ON governance.master_data_items
            FOR EACH ROW EXECUTE FUNCTION governance.audit_master_data_item();
            """);
    }
}
