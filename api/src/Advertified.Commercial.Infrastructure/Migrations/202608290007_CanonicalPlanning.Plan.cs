using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalPlanning
{
    private static void CreateMediaPlanTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.media_plan_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                mix_version_id uuid NOT NULL,
                shortlist_version_id uuid NOT NULL,
                version_no integer NOT NULL,
                subtotal_minor bigint NOT NULL,
                fees_minor bigint NOT NULL,
                vat_minor bigint NOT NULL,
                total_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                forecast_json jsonb NOT NULL,
                assumptions_json jsonb NOT NULL,
                supply_confidence_collection_code varchar(100) NOT NULL
                    DEFAULT 'supplyConfidenceStatuses',
                supply_confidence_code varchar(100) NOT NULL,
                critic_report_json jsonb NOT NULL,
                input_hash char(64) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                approved_by uuid,
                approved_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_media_plan_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_media_plan_version UNIQUE (tenant_id, brief_version_id, version_no),
                CONSTRAINT ck_media_plan_numbers CHECK (
                    version_no > 0 AND subtotal_minor >= 0 AND fees_minor >= 0 AND
                    vat_minor >= 0 AND total_minor = subtotal_minor + fees_minor + vat_minor AND
                    version > 0),
                CONSTRAINT ck_media_plan_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_media_plan_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT ck_media_plan_supply_collection CHECK (
                    supply_confidence_collection_code = 'supplyConfidenceStatuses'),
                CONSTRAINT ck_media_plan_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_media_plan_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_media_plan_mix FOREIGN KEY (tenant_id, mix_version_id)
                    REFERENCES commercial.media_mix_versions (tenant_id, id),
                CONSTRAINT fk_media_plan_shortlist FOREIGN KEY (tenant_id, shortlist_version_id)
                    REFERENCES commercial.inventory_shortlist_versions (tenant_id, id),
                CONSTRAINT fk_media_plan_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_media_plan_supply FOREIGN KEY (
                    supply_confidence_collection_code, supply_confidence_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_media_plan_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_media_plan_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_media_plan_approver FOREIGN KEY (approved_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.media_plan_lines (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                shortlist_candidate_id uuid NOT NULL,
                inventory_product_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                rate_id uuid NOT NULL,
                availability_id uuid,
                flight_start date NOT NULL,
                flight_end date NOT NULL,
                running_periods_json jsonb NOT NULL,
                quantity integer NOT NULL,
                supplier_cost_minor bigint NOT NULL,
                client_price_minor bigint NOT NULL,
                fees_minor bigint NOT NULL,
                vat_minor bigint NOT NULL,
                forecast_json jsonb NOT NULL,
                input_hash char(64) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_media_plan_line_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_media_plan_line_candidate UNIQUE (
                    tenant_id, plan_version_id, shortlist_candidate_id),
                CONSTRAINT ck_media_plan_line_dates CHECK (flight_end >= flight_start),
                CONSTRAINT ck_media_plan_line_numbers CHECK (
                    quantity > 0 AND supplier_cost_minor >= 0 AND client_price_minor >= 0 AND
                    fees_minor >= 0 AND vat_minor >= 0),
                CONSTRAINT ck_media_plan_line_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_media_plan_line_plan FOREIGN KEY (tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_media_plan_line_candidate FOREIGN KEY (
                    tenant_id, shortlist_candidate_id)
                    REFERENCES commercial.inventory_shortlist_candidates (tenant_id, id),
                CONSTRAINT fk_media_plan_line_product FOREIGN KEY (tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_media_plan_line_product_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_media_plan_line_rate FOREIGN KEY (tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_media_plan_line_availability FOREIGN KEY (
                    tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id)
            );
            """);
        CreatePlanGovernanceTables(migrationBuilder);
    }

    private static void CreatePlanGovernanceTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.recommendation_bindings (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                shortlist_version_id uuid NOT NULL,
                shortlist_candidate_id uuid NOT NULL,
                inventory_product_id uuid NOT NULL,
                media_plan_line_id uuid,
                rationale text NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_recommendation_candidate UNIQUE (tenant_id, shortlist_candidate_id),
                CONSTRAINT fk_recommendation_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_recommendation_shortlist FOREIGN KEY (tenant_id, shortlist_version_id)
                    REFERENCES commercial.inventory_shortlist_versions (tenant_id, id),
                CONSTRAINT fk_recommendation_candidate FOREIGN KEY (
                    tenant_id, shortlist_candidate_id)
                    REFERENCES commercial.inventory_shortlist_candidates (tenant_id, id),
                CONSTRAINT fk_recommendation_product FOREIGN KEY (tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_recommendation_line FOREIGN KEY (tenant_id, media_plan_line_id)
                    REFERENCES commercial.media_plan_lines (tenant_id, id),
                CONSTRAINT fk_recommendation_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.supply_coordination (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                media_plan_line_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                availability_collection_code varchar(100) NOT NULL DEFAULT 'availabilityStatuses',
                availability_code varchar(100) NOT NULL,
                rate_freshness_collection_code varchar(100) NOT NULL DEFAULT 'rateFreshnessStatuses',
                rate_freshness_code varchar(100) NOT NULL,
                last_confirmed_at_utc timestamptz,
                source_locator varchar(1000) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_supply_line UNIQUE (tenant_id, media_plan_line_id),
                CONSTRAINT fk_supply_line FOREIGN KEY (tenant_id, media_plan_line_id)
                    REFERENCES commercial.media_plan_lines (tenant_id, id),
                CONSTRAINT fk_supply_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_supply_availability FOREIGN KEY (
                    availability_collection_code, availability_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_supply_rate_freshness FOREIGN KEY (
                    rate_freshness_collection_code, rate_freshness_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_supply_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.planning_objection_resolutions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                objection_code varchar(100) NOT NULL,
                severity_collection_code varchar(100) NOT NULL DEFAULT 'criticSeverities',
                severity_code varchar(100) NOT NULL,
                resolution_collection_code varchar(100) NOT NULL DEFAULT 'objectionResolutions',
                resolution_code varchar(100) NOT NULL,
                reason varchar(2000) NOT NULL,
                resolved_by uuid NOT NULL,
                resolved_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_plan_objection_resolution UNIQUE (
                    tenant_id, plan_version_id, objection_code),
                CONSTRAINT fk_plan_objection_plan FOREIGN KEY (tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_plan_objection_severity FOREIGN KEY (
                    severity_collection_code, severity_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_plan_objection_resolution FOREIGN KEY (
                    resolution_collection_code, resolution_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_plan_objection_actor FOREIGN KEY (resolved_by)
                    REFERENCES commercial.users (id)
            );
            """);
    }
}
