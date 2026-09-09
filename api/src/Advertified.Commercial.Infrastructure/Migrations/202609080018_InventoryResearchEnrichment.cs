using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080018_InventoryResearchEnrichment")]
public sealed class InventoryResearchEnrichment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial.inventory_research_datasets (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            source_name varchar(300) NOT NULL,
            dataset_name varchar(300) NOT NULL,
            measurement_period varchar(200) NOT NULL,
            methodology varchar(2000) NOT NULL,
            universe varchar(1000) NOT NULL,
            rights_reference varchar(1000) NOT NULL,
            taxonomy_name varchar(200),
            taxonomy_version varchar(100),
            limitations varchar(2000),
            status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
            status_code varchar(100) NOT NULL,
            created_by uuid NOT NULL,
            created_at_utc timestamptz NOT NULL,
            version bigint NOT NULL DEFAULT 1,
            CONSTRAINT pk_inventory_research_datasets PRIMARY KEY (id),
            CONSTRAINT ux_inventory_research_dataset_tenant UNIQUE (tenant_id, id),
            CONSTRAINT ck_inventory_research_dataset_status_collection
                CHECK (status_collection_code = 'lifecycleStatuses'),
            CONSTRAINT ck_inventory_research_dataset_version CHECK (version > 0),
            CONSTRAINT ck_inventory_research_dataset_text CHECK (
                btrim(source_name) <> '' AND btrim(dataset_name) <> '' AND
                btrim(measurement_period) <> '' AND btrim(methodology) <> '' AND
                btrim(universe) <> '' AND btrim(rights_reference) <> ''));

        CREATE TABLE commercial.inventory_research_observations (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            dataset_id uuid NOT NULL,
            source_locator varchar(1000) NOT NULL,
            channel_code varchar(100),
            supplier_id uuid,
            supplier_product_code varchar(200),
            outlet_id varchar(64),
            geography varchar(500),
            audience_profile_json jsonb NOT NULL,
            created_at_utc timestamptz NOT NULL,
            CONSTRAINT pk_inventory_research_observations PRIMARY KEY (id),
            CONSTRAINT ux_inventory_research_observation_tenant UNIQUE (tenant_id, id),
            CONSTRAINT ux_inventory_research_observation_locator UNIQUE (tenant_id, dataset_id, source_locator),
            CONSTRAINT fk_inventory_research_observation_dataset FOREIGN KEY (tenant_id, dataset_id)
                REFERENCES commercial.inventory_research_datasets(tenant_id, id),
            CONSTRAINT ck_inventory_research_observation_profile CHECK (
                jsonb_typeof(audience_profile_json) = 'object' AND
                audience_profile_json ?& ARRAY['spokenLanguages','understoodLanguages','lifeStages','lsmSemSegments']),
            CONSTRAINT ck_inventory_research_observation_identity CHECK (
                outlet_id IS NOT NULL OR (supplier_id IS NOT NULL AND supplier_product_code IS NOT NULL)));

        CREATE TABLE commercial.inventory_research_matches (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            dataset_id uuid NOT NULL,
            observation_id uuid NOT NULL,
            product_id uuid,
            product_version_id uuid,
            product_name varchar(500),
            match_basis varchar(100),
            match_detail varchar(1000) NOT NULL,
            status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
            status_code varchar(100) NOT NULL,
            created_by uuid NOT NULL,
            created_at_utc timestamptz NOT NULL,
            reviewed_by uuid,
            reviewed_at_utc timestamptz,
            review_reason varchar(2000),
            applied_product_version_id uuid,
            version bigint NOT NULL DEFAULT 1,
            CONSTRAINT pk_inventory_research_matches PRIMARY KEY (id),
            CONSTRAINT ux_inventory_research_match_tenant UNIQUE (tenant_id, id),
            CONSTRAINT ux_inventory_research_match_observation UNIQUE (tenant_id, observation_id),
            CONSTRAINT fk_inventory_research_match_dataset FOREIGN KEY (tenant_id, dataset_id)
                REFERENCES commercial.inventory_research_datasets(tenant_id, id),
            CONSTRAINT fk_inventory_research_match_observation FOREIGN KEY (tenant_id, observation_id)
                REFERENCES commercial.inventory_research_observations(tenant_id, id),
            CONSTRAINT fk_inventory_research_match_product FOREIGN KEY (tenant_id, product_id)
                REFERENCES commercial.inventory_products(tenant_id, id),
            CONSTRAINT fk_inventory_research_match_product_version FOREIGN KEY (tenant_id, product_version_id)
                REFERENCES commercial.inventory_product_versions(tenant_id, id),
            CONSTRAINT fk_inventory_research_match_applied_version FOREIGN KEY (tenant_id, applied_product_version_id)
                REFERENCES commercial.inventory_product_versions(tenant_id, id),
            CONSTRAINT ck_inventory_research_match_status_collection
                CHECK (status_collection_code = 'lifecycleStatuses'),
            CONSTRAINT ck_inventory_research_match_version CHECK (version > 0),
            CONSTRAINT ck_inventory_research_match_review CHECK (
                (reviewed_by IS NULL AND reviewed_at_utc IS NULL AND review_reason IS NULL) OR
                (reviewed_by IS NOT NULL AND reviewed_at_utc IS NOT NULL AND review_reason IS NOT NULL)));

        CREATE TABLE commercial.inventory_research_portfolios (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            dataset_id uuid NOT NULL,
            source_locator varchar(1000) NOT NULL,
            observation_source_locators_json jsonb NOT NULL,
            deduplicated_reach numeric(18,4) NOT NULL,
            unit_code varchar(100) NOT NULL,
            product_version_ids_json jsonb,
            status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
            status_code varchar(100) NOT NULL,
            CONSTRAINT pk_inventory_research_portfolios PRIMARY KEY (id),
            CONSTRAINT ux_inventory_research_portfolio_tenant UNIQUE (tenant_id, id),
            CONSTRAINT ux_inventory_research_portfolio_locator UNIQUE (tenant_id, dataset_id, source_locator),
            CONSTRAINT fk_inventory_research_portfolio_dataset FOREIGN KEY (tenant_id, dataset_id)
                REFERENCES commercial.inventory_research_datasets(tenant_id, id),
            CONSTRAINT ck_inventory_research_portfolio_status_collection
                CHECK (status_collection_code = 'lifecycleStatuses'),
            CONSTRAINT ck_inventory_research_portfolio_locators
                CHECK (jsonb_typeof(observation_source_locators_json) = 'array' AND
                       jsonb_array_length(observation_source_locators_json) >= 2),
            CONSTRAINT ck_inventory_research_portfolio_products
                CHECK (product_version_ids_json IS NULL OR jsonb_typeof(product_version_ids_json) = 'array'),
            CONSTRAINT ck_inventory_research_portfolio_reach CHECK (deduplicated_reach >= 0));

        CREATE INDEX ix_inventory_research_observation_outlet
            ON commercial.inventory_research_observations (tenant_id, outlet_id)
            WHERE outlet_id IS NOT NULL;
        CREATE INDEX ix_inventory_research_match_status
            ON commercial.inventory_research_matches (tenant_id, dataset_id, status_code, id);

        ALTER TABLE commercial.inventory_research_datasets ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_research_datasets FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_research_datasets_tenant_scope
            ON commercial.inventory_research_datasets
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        ALTER TABLE commercial.inventory_research_observations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_research_observations FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_research_observations_tenant_scope
            ON commercial.inventory_research_observations
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        ALTER TABLE commercial.inventory_research_matches ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_research_matches FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_research_matches_tenant_scope
            ON commercial.inventory_research_matches
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        ALTER TABLE commercial.inventory_research_portfolios ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_research_portfolios FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_research_portfolios_tenant_scope
            ON commercial.inventory_research_portfolios
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        GRANT SELECT, INSERT, UPDATE ON commercial.inventory_research_datasets TO advertified_app;
        GRANT SELECT, INSERT ON commercial.inventory_research_observations TO advertified_app;
        GRANT SELECT, INSERT, UPDATE ON commercial.inventory_research_matches TO advertified_app;
        GRANT SELECT, INSERT, UPDATE ON commercial.inventory_research_portfolios TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Approved research enrichment evidence is forward-only.");
}
