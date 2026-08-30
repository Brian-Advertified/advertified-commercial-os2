using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void CreateCreativeRequirementAndAssetTables(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            CREATE TABLE commercial.creative_requirements (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                booking_id uuid NOT NULL,
                media_plan_line_id uuid NOT NULL,
                channel_collection_code varchar(100) NOT NULL DEFAULT 'channels',
                channel_code varchar(100) NOT NULL,
                flight_start date NOT NULL,
                flight_end date NOT NULL,
                format_code varchar(100) NOT NULL,
                width integer NOT NULL,
                height integer NOT NULL,
                required_media_type varchar(100) NOT NULL,
                maximum_bytes integer NOT NULL,
                instructions varchar(2000) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_creative_requirement_buyer_id UNIQUE (buyer_tenant_id, id),
                CONSTRAINT ux_creative_requirement_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_creative_requirement_booking UNIQUE (
                    buyer_tenant_id, campaign_id, booking_id),
                CONSTRAINT ck_creative_requirement_tenants CHECK (
                    buyer_tenant_id <> supplier_tenant_id),
                CONSTRAINT ck_creative_requirement_dates CHECK (flight_end >= flight_start),
                CONSTRAINT ck_creative_requirement_dimensions CHECK (
                    width > 0 AND width <= 20000 AND height > 0 AND height <= 20000),
                CONSTRAINT ck_creative_requirement_size CHECK (
                    maximum_bytes > 0 AND maximum_bytes <= 104857600),
                CONSTRAINT ck_creative_requirement_media CHECK (
                    required_media_type IN ('image/png', 'image/jpeg', 'application/pdf')),
                CONSTRAINT ck_creative_requirement_channel_collection CHECK (
                    channel_collection_code = 'channels'),
                CONSTRAINT fk_creative_requirement_buyer FOREIGN KEY (buyer_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_creative_requirement_supplier FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_creative_requirement_campaign FOREIGN KEY (
                    buyer_tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_creative_requirement_booking FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, booking_id)
                    REFERENCES commercial.bookings (buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_requirement_line FOREIGN KEY (
                    buyer_tenant_id, media_plan_line_id)
                    REFERENCES commercial.media_plan_lines (tenant_id, id),
                CONSTRAINT fk_creative_requirement_channel FOREIGN KEY (
                    channel_collection_code, channel_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_creative_requirement_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.creative_assets (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                requirement_id uuid NOT NULL,
                current_version_id uuid,
                version bigint NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_creative_asset_buyer_id UNIQUE (buyer_tenant_id, id),
                CONSTRAINT ux_creative_asset_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_creative_asset_requirement UNIQUE (
                    buyer_tenant_id, requirement_id),
                CONSTRAINT ck_creative_asset_tenants CHECK (
                    buyer_tenant_id <> supplier_tenant_id),
                CONSTRAINT ck_creative_asset_version CHECK (version >= 0),
                CONSTRAINT fk_creative_asset_buyer FOREIGN KEY (buyer_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_creative_asset_supplier FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_creative_asset_campaign FOREIGN KEY (
                    buyer_tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_creative_asset_requirement FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, requirement_id)
                    REFERENCES commercial.creative_requirements (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_asset_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_creative_requirement_campaign
                ON commercial.creative_requirements (buyer_tenant_id, campaign_id, id);
            CREATE INDEX ix_creative_asset_supplier
                ON commercial.creative_assets (supplier_tenant_id, updated_at_utc DESC, id);
            """);
}
