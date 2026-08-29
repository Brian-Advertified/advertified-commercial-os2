using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryTruth
{
    private static void CreateInventoryProductTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_products (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                supplier_product_code varchar(200) NOT NULL,
                current_version_id uuid,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_products_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_products_supplier_code UNIQUE (
                    tenant_id, supplier_id, supplier_product_code),
                CONSTRAINT ck_inventory_product_version CHECK (version > 0),
                CONSTRAINT ck_inventory_product_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_inventory_product_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_inventory_product_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_inventory_product_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.inventory_product_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                product_id uuid NOT NULL,
                version_number integer NOT NULL,
                name varchar(500) NOT NULL,
                channel_collection_code varchar(100) NOT NULL DEFAULT 'channels',
                channel_code varchar(100) NOT NULL,
                product_type_collection_code varchar(100) NOT NULL DEFAULT 'inventoryProductTypes',
                product_type_code varchar(100) NOT NULL,
                geography varchar(500) NOT NULL,
                address varchar(1000),
                latitude numeric(9,6),
                longitude numeric(9,6),
                extension_json jsonb NOT NULL DEFAULT '{}',
                verification_collection_code varchar(100) NOT NULL DEFAULT 'verificationLevels',
                verification_code varchar(100) NOT NULL,
                source_import_id uuid NOT NULL,
                source_candidate_id uuid NOT NULL,
                published_by uuid NOT NULL,
                published_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_product_versions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_product_versions_number UNIQUE (
                    tenant_id, product_id, version_number),
                CONSTRAINT ck_inventory_product_version_number CHECK (version_number > 0),
                CONSTRAINT ck_inventory_product_coordinates CHECK (
                    (latitude IS NULL AND longitude IS NULL) OR
                    (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)),
                CONSTRAINT ck_inventory_product_channel_collection CHECK (
                    channel_collection_code = 'channels'),
                CONSTRAINT ck_inventory_product_type_collection CHECK (
                    product_type_collection_code = 'inventoryProductTypes'),
                CONSTRAINT ck_inventory_product_verification_collection CHECK (
                    verification_collection_code = 'verificationLevels'),
                CONSTRAINT fk_inventory_product_version_product FOREIGN KEY (tenant_id, product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_inventory_product_version_channel FOREIGN KEY (
                    channel_collection_code, channel_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_product_version_type FOREIGN KEY (
                    product_type_collection_code, product_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_product_version_verification FOREIGN KEY (
                    verification_collection_code, verification_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_product_version_import FOREIGN KEY (tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id),
                CONSTRAINT fk_inventory_product_version_candidate FOREIGN KEY (tenant_id, source_candidate_id)
                    REFERENCES commercial.inventory_candidates (tenant_id, id),
                CONSTRAINT fk_inventory_product_version_publisher FOREIGN KEY (published_by)
                    REFERENCES commercial.users (id)
            );
            ALTER TABLE commercial.inventory_products
                ADD CONSTRAINT fk_inventory_products_current_version
                FOREIGN KEY (tenant_id, current_version_id)
                REFERENCES commercial.inventory_product_versions (tenant_id, id);
            CREATE INDEX ix_inventory_product_search
                ON commercial.inventory_product_versions (tenant_id, channel_code, geography, name, id);
            """);
        CreateInventoryFactTables(migrationBuilder);
    }

    private static void CreateInventoryFactTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_rates (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                rate_type_collection_code varchar(100) NOT NULL DEFAULT 'rateTypes',
                rate_type_code varchar(100) NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                amount_minor bigint NOT NULL,
                effective_from date,
                effective_to date,
                source_locator varchar(1000) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ck_inventory_rate_amount CHECK (amount_minor >= 0),
                CONSTRAINT ck_inventory_rate_dates CHECK (
                    effective_to IS NULL OR effective_from IS NULL OR effective_to > effective_from),
                CONSTRAINT ck_inventory_rate_type_collection CHECK (rate_type_collection_code = 'rateTypes'),
                CONSTRAINT ck_inventory_rate_currency_collection CHECK (currency_collection_code = 'currencies'),
                CONSTRAINT fk_inventory_rate_version FOREIGN KEY (tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_rate_type FOREIGN KEY (rate_type_collection_code, rate_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_rate_currency FOREIGN KEY (currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.inventory_availability (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                availability_collection_code varchar(100) NOT NULL DEFAULT 'availabilityStatuses',
                availability_code varchar(100) NOT NULL,
                observed_at_utc timestamptz,
                valid_until_utc timestamptz,
                source_locator varchar(1000) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ck_inventory_availability_collection CHECK (
                    availability_collection_code = 'availabilityStatuses'),
                CONSTRAINT ck_inventory_availability_dates CHECK (
                    valid_until_utc IS NULL OR observed_at_utc IS NULL OR valid_until_utc > observed_at_utc),
                CONSTRAINT fk_inventory_availability_version FOREIGN KEY (tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_availability_status FOREIGN KEY (
                    availability_collection_code, availability_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.inventory_assets (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                asset_type_collection_code varchar(100) NOT NULL DEFAULT 'assetTypes',
                asset_type_code varchar(100) NOT NULL,
                object_key varchar(1000) NOT NULL,
                content_hash char(64) NOT NULL,
                media_type varchar(200) NOT NULL,
                source_import_id uuid NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ck_inventory_asset_hash CHECK (content_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_asset_type_collection CHECK (asset_type_collection_code = 'assetTypes'),
                CONSTRAINT fk_inventory_asset_version FOREIGN KEY (tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_asset_type FOREIGN KEY (asset_type_collection_code, asset_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_asset_import FOREIGN KEY (tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id)
            );
            """);
    }
}
