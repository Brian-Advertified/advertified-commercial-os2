using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class StructuredInventory
{
    private static void CreateSupplierCommercialTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_supplier_versions (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                version_number integer NOT NULL,
                vat_status_collection_code varchar(100) DEFAULT 'vatStatuses',
                vat_status_code varchar(100),
                vat_number varchar(100),
                commission_terms varchar(2000),
                payment_terms varchar(2000),
                cancellation_terms varchar(2000),
                booking_deadline_terms varchar(2000),
                source_import_id uuid NOT NULL,
                published_by uuid NOT NULL,
                published_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_supplier_versions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_supplier_versions_number UNIQUE (
                    tenant_id, supplier_id, version_number),
                CONSTRAINT ck_inventory_supplier_version_number CHECK (version_number > 0),
                CONSTRAINT ck_inventory_supplier_vat_collection CHECK (
                    vat_status_code IS NULL OR vat_status_collection_code = 'vatStatuses'),
                CONSTRAINT fk_inventory_supplier_version_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_inventory_supplier_version_vat FOREIGN KEY (
                    vat_status_collection_code, vat_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_supplier_version_import FOREIGN KEY (tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id),
                CONSTRAINT fk_inventory_supplier_version_publisher FOREIGN KEY (published_by)
                    REFERENCES commercial.users (id));

            ALTER TABLE commercial.inventory_suppliers
                ADD COLUMN current_commercial_version_id uuid,
                ADD CONSTRAINT fk_inventory_supplier_current_commercial_version
                    FOREIGN KEY (tenant_id, current_commercial_version_id)
                    REFERENCES commercial.inventory_supplier_versions (tenant_id, id);

            CREATE TABLE commercial.inventory_supplier_contacts (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                name varchar(300),
                role varchar(200),
                region varchar(200),
                email varchar(320),
                phone varchar(100),
                website varchar(1000),
                social_handle varchar(300),
                source_import_id uuid NOT NULL,
                observed_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_supplier_contacts_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_inventory_supplier_contact_value CHECK (
                    name IS NOT NULL OR email IS NOT NULL OR phone IS NOT NULL OR
                    website IS NOT NULL OR social_handle IS NOT NULL),
                CONSTRAINT ck_inventory_supplier_contact_email CHECK (
                    email IS NULL OR email = lower(email)),
                CONSTRAINT fk_inventory_supplier_contact_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_inventory_supplier_contact_import FOREIGN KEY (tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id));
            CREATE INDEX ix_inventory_supplier_contacts_current
                ON commercial.inventory_supplier_contacts (tenant_id, supplier_id, observed_at_utc DESC);
            """);

    private static void AddStructuredProductFields(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_product_versions
                ADD COLUMN description varchar(4000),
                ADD COLUMN deliverable_json jsonb,
                ADD COLUMN spatial_json jsonb,
                ADD COLUMN coverage_geometry geometry(MultiPolygon, 4326),
                ADD COLUMN catchment_geometry geometry(MultiPolygon, 4326),
                ADD COLUMN route_geometry geometry(MultiLineString, 4326),
                ADD COLUMN direction_geometry geometry(LineString, 4326),
                ADD CONSTRAINT ck_inventory_deliverable_json CHECK (
                    deliverable_json IS NULL OR jsonb_typeof(deliverable_json) = 'object'),
                ADD CONSTRAINT ck_inventory_spatial_json CHECK (
                    spatial_json IS NULL OR jsonb_typeof(spatial_json) = 'object');
            CREATE INDEX ix_inventory_product_coverage_geometry
                ON commercial.inventory_product_versions USING GIST (coverage_geometry)
                WHERE coverage_geometry IS NOT NULL;
            CREATE INDEX ix_inventory_product_catchment_geometry
                ON commercial.inventory_product_versions USING GIST (catchment_geometry)
                WHERE catchment_geometry IS NOT NULL;
            CREATE INDEX ix_inventory_product_route_geometry
                ON commercial.inventory_product_versions USING GIST (route_geometry)
                WHERE route_geometry IS NOT NULL;
            CREATE INDEX ix_inventory_product_direction_geometry
                ON commercial.inventory_product_versions USING GIST (direction_geometry)
                WHERE direction_geometry IS NOT NULL;

            ALTER TABLE commercial.inventory_rates
                ADD COLUMN vat_treatment_collection_code varchar(100) DEFAULT 'vatTreatments',
                ADD COLUMN vat_treatment_code varchar(100),
                ADD COLUMN commercial_terms_json jsonb,
                ADD CONSTRAINT ck_inventory_rate_vat_treatment_collection CHECK (
                    vat_treatment_code IS NULL OR
                    vat_treatment_collection_code = 'vatTreatments'),
                ADD CONSTRAINT ck_inventory_rate_commercial_terms CHECK (
                    commercial_terms_json IS NULL OR
                    jsonb_typeof(commercial_terms_json) = 'object'),
                ADD CONSTRAINT fk_inventory_rate_vat_treatment FOREIGN KEY (
                    vat_treatment_collection_code, vat_treatment_code)
                    REFERENCES governance.master_data_items (collection_code, code);

            ALTER TABLE commercial.marketplace_listing_versions
                ADD COLUMN supplier_vat_status_collection_code varchar(100)
                    DEFAULT 'vatStatuses',
                ADD COLUMN supplier_vat_status_code varchar(100),
                ADD COLUMN supplier_commercial_json jsonb,
                ADD COLUMN vat_treatment_collection_code varchar(100)
                    DEFAULT 'vatTreatments',
                ADD COLUMN vat_treatment_code varchar(100),
                ADD COLUMN commercial_terms_json jsonb,
                ADD COLUMN deliverable_json jsonb,
                ADD COLUMN spatial_json jsonb,
                ADD COLUMN logo_asset_id uuid,
                ADD CONSTRAINT fk_marketplace_supplier_vat_status FOREIGN KEY (
                    supplier_vat_status_collection_code, supplier_vat_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_marketplace_vat_treatment FOREIGN KEY (
                    vat_treatment_collection_code, vat_treatment_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_marketplace_logo_asset FOREIGN KEY (
                    supplier_tenant_id, logo_asset_id)
                    REFERENCES commercial.inventory_assets (tenant_id, id),
                ADD CONSTRAINT ck_marketplace_vat_collections CHECK (
                    (supplier_vat_status_code IS NULL OR
                        supplier_vat_status_collection_code = 'vatStatuses') AND
                    (vat_treatment_code IS NULL OR
                        vat_treatment_collection_code = 'vatTreatments')),
                ADD CONSTRAINT ck_marketplace_structured_inventory CHECK (
                    (supplier_commercial_json IS NULL OR
                        jsonb_typeof(supplier_commercial_json) = 'object') AND
                    (commercial_terms_json IS NULL OR
                        jsonb_typeof(commercial_terms_json) = 'object') AND
                    (deliverable_json IS NULL OR jsonb_typeof(deliverable_json) = 'object') AND
                    (spatial_json IS NULL OR jsonb_typeof(spatial_json) = 'object'));

            ALTER TABLE commercial.inventory_shortlist_candidates
                ADD COLUMN commercial_readiness_json jsonb NOT NULL DEFAULT
                    '{"supplierVatStatus":null,"vatTreatment":null,"evidenceGaps":["inventory.supplierCommercial.vatStatus","inventory.rate.vatTreatment"]}',
                ADD COLUMN deliverable_json jsonb,
                ADD COLUMN spatial_json jsonb,
                ADD COLUMN supplier_commercial_json jsonb,
                ADD COLUMN commercial_terms_json jsonb,
                ADD COLUMN logo_asset_id uuid,
                ADD CONSTRAINT ck_shortlist_commercial_readiness CHECK (
                    jsonb_typeof(commercial_readiness_json) = 'object' AND
                    jsonb_typeof(commercial_readiness_json->'evidenceGaps') = 'array'),
                ADD CONSTRAINT ck_shortlist_structured_inventory CHECK (
                    (supplier_commercial_json IS NULL OR
                        jsonb_typeof(supplier_commercial_json) = 'object') AND
                    (commercial_terms_json IS NULL OR
                        jsonb_typeof(commercial_terms_json) = 'object') AND
                    (deliverable_json IS NULL OR jsonb_typeof(deliverable_json) = 'object') AND
                    (spatial_json IS NULL OR jsonb_typeof(spatial_json) = 'object'));

            ALTER TABLE commercial.media_plan_versions
                ADD COLUMN commercial_policy_version_id uuid;
            UPDATE commercial.media_plan_versions plan
            SET commercial_policy_version_id = policy.current_version_id
            FROM commercial.commercial_policies policy
            WHERE policy.tenant_id = plan.tenant_id
              AND policy.current_version_id IS NOT NULL;
            ALTER TABLE commercial.media_plan_versions
                ADD CONSTRAINT fk_media_plan_commercial_policy FOREIGN KEY (
                    tenant_id, commercial_policy_version_id)
                    REFERENCES commercial.commercial_policy_versions (tenant_id, id);

            ALTER TABLE commercial.media_plan_lines
                ADD COLUMN supplier_commercial_json jsonb,
                ADD COLUMN vat_treatment_collection_code varchar(100)
                    DEFAULT 'vatTreatments',
                ADD COLUMN vat_treatment_code varchar(100),
                ADD COLUMN commercial_terms_json jsonb,
                ADD COLUMN deliverable_json jsonb,
                ADD COLUMN spatial_json jsonb,
                ADD COLUMN logo_asset_id uuid,
                ADD CONSTRAINT fk_media_plan_line_vat_treatment FOREIGN KEY (
                    vat_treatment_collection_code, vat_treatment_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_media_plan_line_logo_asset FOREIGN KEY (
                    inventory_tenant_id, logo_asset_id)
                    REFERENCES commercial.inventory_assets (tenant_id, id),
                ADD CONSTRAINT ck_media_plan_line_structured_inventory CHECK (
                    (vat_treatment_code IS NULL OR
                        vat_treatment_collection_code = 'vatTreatments') AND
                    (supplier_commercial_json IS NULL OR
                        jsonb_typeof(supplier_commercial_json) = 'object') AND
                    (commercial_terms_json IS NULL OR
                        jsonb_typeof(commercial_terms_json) = 'object') AND
                    (deliverable_json IS NULL OR jsonb_typeof(deliverable_json) = 'object') AND
                    (spatial_json IS NULL OR jsonb_typeof(spatial_json) = 'object'));

            ALTER TABLE commercial.bookings
                ADD COLUMN supplier_commercial_json jsonb,
                ADD COLUMN vat_treatment_collection_code varchar(100)
                    DEFAULT 'vatTreatments',
                ADD COLUMN vat_treatment_code varchar(100),
                ADD COLUMN commercial_terms_json jsonb,
                ADD COLUMN deliverable_json jsonb,
                ADD COLUMN spatial_json jsonb,
                ADD COLUMN logo_asset_id uuid,
                ADD CONSTRAINT fk_booking_vat_treatment FOREIGN KEY (
                    vat_treatment_collection_code, vat_treatment_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_booking_logo_asset FOREIGN KEY (
                    supplier_tenant_id, logo_asset_id)
                    REFERENCES commercial.inventory_assets (tenant_id, id),
                ADD CONSTRAINT ck_booking_structured_inventory CHECK (
                    (vat_treatment_code IS NULL OR
                        vat_treatment_collection_code = 'vatTreatments') AND
                    (supplier_commercial_json IS NULL OR
                        jsonb_typeof(supplier_commercial_json) = 'object') AND
                    (commercial_terms_json IS NULL OR
                        jsonb_typeof(commercial_terms_json) = 'object') AND
                    (deliverable_json IS NULL OR jsonb_typeof(deliverable_json) = 'object') AND
                    (spatial_json IS NULL OR jsonb_typeof(spatial_json) = 'object'));
            ALTER TABLE commercial.bookings
                DROP CONSTRAINT ck_booking_amounts,
                ADD CONSTRAINT ck_booking_amounts CHECK (
                    fees_minor = markup_minor + commission_minor + management_fee_minor
                    AND (client_price_minor = supplier_cost_minor + fees_minor + vat_minor
                        OR client_price_minor = supplier_cost_minor + fees_minor));
            """);

    private static void CreatePackageAndAssetRightsTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_assets
                ADD CONSTRAINT ux_inventory_assets_tenant_id UNIQUE (tenant_id, id);

            CREATE TABLE commercial.inventory_packages (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                package_code varchar(200) NOT NULL,
                version_number integer NOT NULL,
                name varchar(500) NOT NULL,
                rate_id uuid NOT NULL,
                discount_rule varchar(2000),
                conditions_json jsonb NOT NULL DEFAULT '[]',
                source_import_id uuid NOT NULL,
                CONSTRAINT ux_inventory_packages_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_packages_version UNIQUE (
                    tenant_id, supplier_id, package_code, version_number),
                CONSTRAINT ux_inventory_packages_source UNIQUE (
                    tenant_id, supplier_id, package_code, source_import_id),
                CONSTRAINT ck_inventory_package_version CHECK (version_number > 0),
                CONSTRAINT ck_inventory_package_conditions CHECK (
                    jsonb_typeof(conditions_json) = 'array'),
                CONSTRAINT fk_inventory_package_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_inventory_package_rate FOREIGN KEY (tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_inventory_package_import FOREIGN KEY (tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id));

            CREATE TABLE commercial.inventory_package_components (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                package_id uuid NOT NULL,
                product_id uuid NOT NULL,
                CONSTRAINT ux_inventory_package_component UNIQUE (tenant_id, package_id, product_id),
                CONSTRAINT fk_inventory_package_component_package FOREIGN KEY (tenant_id, package_id)
                    REFERENCES commercial.inventory_packages (tenant_id, id),
                CONSTRAINT fk_inventory_package_component_product FOREIGN KEY (tenant_id, product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id));

            CREATE TABLE commercial.inventory_asset_rights_reviews (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                asset_id uuid NOT NULL,
                asset_version bigint NOT NULL,
                rights_collection_code varchar(100) NOT NULL DEFAULT 'assetRightsStatuses',
                rights_status_code varchar(100) NOT NULL,
                rights_basis varchar(1000),
                licensed_until date,
                reviewed_by uuid NOT NULL,
                reviewed_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_asset_rights_review UNIQUE (
                    tenant_id, asset_id, asset_version),
                CONSTRAINT ck_inventory_asset_rights_version CHECK (asset_version > 0),
                CONSTRAINT ck_inventory_asset_rights_collection CHECK (
                    rights_collection_code = 'assetRightsStatuses'),
                CONSTRAINT fk_inventory_asset_rights_asset FOREIGN KEY (tenant_id, asset_id)
                    REFERENCES commercial.inventory_assets (tenant_id, id),
                CONSTRAINT fk_inventory_asset_rights_status FOREIGN KEY (
                    rights_collection_code, rights_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_asset_rights_reviewer FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id));
            """);

    private static void CreateSpatialEvidenceTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_product_points_of_interest (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                name varchar(500) NOT NULL,
                category varchar(200),
                location geography(Point, 4326),
                source_import_id uuid NOT NULL,
                CONSTRAINT ux_inventory_product_poi_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_product_poi_value UNIQUE NULLS NOT DISTINCT (
                    tenant_id, product_version_id, name, category),
                CONSTRAINT ck_inventory_product_poi_name CHECK (btrim(name) <> ''),
                CONSTRAINT fk_inventory_product_poi_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_product_poi_import FOREIGN KEY (
                    tenant_id, source_import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id));
            CREATE INDEX ix_inventory_product_poi_location
                ON commercial.inventory_product_points_of_interest USING GIST (location)
                WHERE location IS NOT NULL;
            """);
}
