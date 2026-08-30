using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class SupplierMarketplace
{
    private static void CreateMarketplaceTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.marketplace_listings (
                id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                product_id uuid NOT NULL,
                current_version_id uuid,
                status_collection_code varchar(100) NOT NULL DEFAULT 'marketplaceListingStatuses',
                status_code varchar(100) NOT NULL,
                terms varchar(5000) NOT NULL,
                archived_reason varchar(1000),
                created_by uuid NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_marketplace_listings_tenant_id UNIQUE (supplier_tenant_id, id),
                CONSTRAINT ux_marketplace_listing_product UNIQUE (supplier_tenant_id, product_id),
                CONSTRAINT ck_marketplace_listing_version CHECK (version > 0),
                CONSTRAINT ck_marketplace_listing_status_collection CHECK (
                    status_collection_code = 'marketplaceListingStatuses'),
                CONSTRAINT fk_marketplace_listing_tenant FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_marketplace_listing_product FOREIGN KEY (supplier_tenant_id, product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_marketplace_listing_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_marketplace_listing_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.marketplace_listing_versions (
                id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                listing_id uuid NOT NULL,
                version_number integer NOT NULL,
                product_version_id uuid NOT NULL,
                rate_id uuid NOT NULL,
                availability_id uuid NOT NULL,
                supplier_name varchar(500) NOT NULL,
                product_name varchar(500) NOT NULL,
                channel_code varchar(100) NOT NULL,
                product_type_code varchar(100) NOT NULL,
                geography varchar(500) NOT NULL,
                rate_type_code varchar(100) NOT NULL,
                amount_minor bigint NOT NULL,
                currency_code varchar(100) NOT NULL,
                availability_code varchar(100) NOT NULL,
                availability_valid_until_utc timestamptz,
                terms varchar(5000) NOT NULL,
                published_by uuid NOT NULL,
                published_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_marketplace_listing_versions_tenant_id UNIQUE (supplier_tenant_id, id),
                CONSTRAINT ux_marketplace_listing_version_number UNIQUE (
                    supplier_tenant_id, listing_id, version_number),
                CONSTRAINT ck_marketplace_listing_version_number CHECK (version_number > 0),
                CONSTRAINT ck_marketplace_listing_amount CHECK (amount_minor >= 0),
                CONSTRAINT fk_marketplace_listing_version_listing FOREIGN KEY (
                    supplier_tenant_id, listing_id)
                    REFERENCES commercial.marketplace_listings (supplier_tenant_id, id),
                CONSTRAINT fk_marketplace_listing_version_product FOREIGN KEY (
                    supplier_tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_marketplace_listing_version_rate FOREIGN KEY (
                    supplier_tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_marketplace_listing_version_availability FOREIGN KEY (
                    supplier_tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                CONSTRAINT fk_marketplace_listing_version_publisher FOREIGN KEY (published_by)
                    REFERENCES commercial.users (id)
            );
            ALTER TABLE commercial.marketplace_listings
                ADD CONSTRAINT fk_marketplace_listing_current_version
                FOREIGN KEY (supplier_tenant_id, current_version_id)
                REFERENCES commercial.marketplace_listing_versions (supplier_tenant_id, id);

            CREATE TABLE commercial.marketplace_rfqs (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                listing_version_id uuid NOT NULL,
                subject varchar(500) NOT NULL,
                requested_start date NOT NULL,
                requested_end date NOT NULL,
                quantity integer NOT NULL,
                due_at_utc timestamptz NOT NULL,
                created_by uuid NOT NULL,
                sent_by uuid,
                sent_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_marketplace_rfqs_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ck_marketplace_rfq_distinct_tenants CHECK (
                    buyer_tenant_id <> supplier_tenant_id),
                CONSTRAINT ck_marketplace_rfq_dates CHECK (
                    requested_end >= requested_start),
                CONSTRAINT ck_marketplace_rfq_quantity CHECK (quantity > 0),
                CONSTRAINT ck_marketplace_rfq_version CHECK (version > 0),
                CONSTRAINT fk_marketplace_rfq_buyer FOREIGN KEY (buyer_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_marketplace_rfq_supplier FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_marketplace_rfq_listing FOREIGN KEY (
                    supplier_tenant_id, listing_version_id)
                    REFERENCES commercial.marketplace_listing_versions (supplier_tenant_id, id),
                CONSTRAINT fk_marketplace_rfq_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_marketplace_rfq_sender FOREIGN KEY (sent_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.marketplace_supplier_responses (
                id uuid NOT NULL,
                rfq_id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                response_version integer NOT NULL,
                amount_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                availability_collection_code varchar(100) NOT NULL DEFAULT 'availabilityStatuses',
                availability_code varchar(100) NOT NULL,
                terms varchar(5000) NOT NULL,
                valid_until_utc timestamptz NOT NULL,
                evidence_references_json jsonb NOT NULL DEFAULT '[]',
                submitted_by uuid NOT NULL,
                submitted_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_marketplace_response_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_marketplace_response_version UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, rfq_id, response_version),
                CONSTRAINT ck_marketplace_response_version CHECK (response_version > 0),
                CONSTRAINT ck_marketplace_response_amount CHECK (amount_minor >= 0),
                CONSTRAINT ck_marketplace_response_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT ck_marketplace_response_availability_collection CHECK (
                    availability_collection_code = 'availabilityStatuses'),
                CONSTRAINT fk_marketplace_response_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_marketplace_response_availability FOREIGN KEY (
                    availability_collection_code, availability_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_marketplace_response_rfq FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, rfq_id)
                    REFERENCES commercial.marketplace_rfqs (buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_marketplace_response_submitter FOREIGN KEY (submitted_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.marketplace_response_acceptances (
                id uuid NOT NULL,
                response_id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                reason varchar(1000) NOT NULL,
                accepted_by uuid NOT NULL,
                accepted_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_marketplace_response_acceptance UNIQUE (response_id),
                CONSTRAINT fk_marketplace_acceptance_response FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, response_id)
                    REFERENCES commercial.marketplace_supplier_responses (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_marketplace_acceptance_actor FOREIGN KEY (accepted_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_marketplace_listing_search ON commercial.marketplace_listing_versions (
                lower(product_name), channel_code, geography, id);
            CREATE INDEX ix_marketplace_rfq_buyer_queue ON commercial.marketplace_rfqs (
                buyer_tenant_id, updated_at_utc DESC, id);
            CREATE INDEX ix_marketplace_rfq_supplier_queue ON commercial.marketplace_rfqs (
                supplier_tenant_id, updated_at_utc DESC, id);
            """);
    }
}
