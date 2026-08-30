using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class SelectedOptionBooking
{
    private static void CreateBookingTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.proposal_decisions
                ADD CONSTRAINT ux_proposal_decision_tenant_id UNIQUE (tenant_id, id);

            CREATE TABLE commercial.bookings (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                proposal_option_id uuid NOT NULL,
                proposal_decision_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                media_plan_line_id uuid NOT NULL,
                marketplace_listing_version_id uuid NOT NULL,
                commercial_policy_version_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                inventory_product_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                rate_id uuid NOT NULL,
                availability_id uuid NOT NULL,
                supplier_name varchar(500) NOT NULL,
                product_name varchar(500) NOT NULL,
                channel_code varchar(100) NOT NULL,
                geography varchar(500) NOT NULL,
                flight_start date NOT NULL,
                flight_end date NOT NULL,
                running_periods integer NOT NULL,
                quantity integer NOT NULL,
                supplier_cost_minor bigint NOT NULL,
                markup_minor bigint NOT NULL,
                commission_minor bigint NOT NULL,
                management_fee_minor bigint NOT NULL,
                client_price_minor bigint NOT NULL,
                fees_minor bigint NOT NULL,
                vat_minor bigint NOT NULL,
                booking_approval_threshold_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                terms varchar(5000) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                requested_by uuid,
                requested_at_utc timestamptz,
                request_reason varchar(1000),
                confirmed_by uuid,
                confirmed_at_utc timestamptz,
                confirmation_reason varchar(1000),
                supplier_note varchar(2000),
                terms_accepted boolean NOT NULL DEFAULT false,
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_booking_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_booking_selected_line UNIQUE (
                    buyer_tenant_id, proposal_decision_id, media_plan_line_id),
                CONSTRAINT ck_booking_distinct_tenants CHECK (
                    buyer_tenant_id <> supplier_tenant_id),
                CONSTRAINT ck_booking_dates CHECK (flight_end >= flight_start),
                CONSTRAINT ck_booking_numbers CHECK (
                    running_periods > 0 AND quantity > 0 AND version > 0
                    AND supplier_cost_minor >= 0 AND markup_minor >= 0
                    AND commission_minor >= 0 AND management_fee_minor >= 0
                    AND client_price_minor >= 0 AND fees_minor >= 0 AND vat_minor >= 0
                    AND booking_approval_threshold_minor >= 0),
                CONSTRAINT ck_booking_amounts CHECK (
                    fees_minor = markup_minor + commission_minor + management_fee_minor
                    AND client_price_minor = supplier_cost_minor + fees_minor + vat_minor),
                CONSTRAINT ck_booking_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT ck_booking_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_booking_status_shape CHECK (
                    (status_code = 'DRAFT' AND requested_by IS NULL
                        AND requested_at_utc IS NULL AND request_reason IS NULL
                        AND confirmed_by IS NULL AND confirmed_at_utc IS NULL
                        AND confirmation_reason IS NULL AND supplier_note IS NULL
                        AND terms_accepted = false)
                    OR (status_code = 'PENDING_SUPPLIER' AND requested_by IS NOT NULL
                        AND requested_at_utc IS NOT NULL AND request_reason IS NOT NULL
                        AND confirmed_by IS NULL AND confirmed_at_utc IS NULL
                        AND confirmation_reason IS NULL AND supplier_note IS NULL
                        AND terms_accepted = false)
                    OR (status_code = 'CONFIRMED' AND requested_by IS NOT NULL
                        AND requested_at_utc IS NOT NULL AND request_reason IS NOT NULL
                        AND confirmed_by IS NOT NULL AND confirmed_at_utc IS NOT NULL
                        AND confirmation_reason IS NOT NULL AND terms_accepted = true)),
                CONSTRAINT fk_booking_buyer FOREIGN KEY (buyer_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_booking_supplier_tenant FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_booking_proposal FOREIGN KEY (
                    buyer_tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_booking_option FOREIGN KEY (
                    buyer_tenant_id, proposal_option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_booking_decision FOREIGN KEY (
                    buyer_tenant_id, proposal_decision_id)
                    REFERENCES commercial.proposal_decisions (tenant_id, id),
                CONSTRAINT fk_booking_plan FOREIGN KEY (
                    buyer_tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_booking_plan_line FOREIGN KEY (
                    buyer_tenant_id, media_plan_line_id)
                    REFERENCES commercial.media_plan_lines (tenant_id, id),
                CONSTRAINT fk_booking_listing FOREIGN KEY (
                    supplier_tenant_id, marketplace_listing_version_id)
                    REFERENCES commercial.marketplace_listing_versions (supplier_tenant_id, id),
                CONSTRAINT fk_booking_policy FOREIGN KEY (
                    buyer_tenant_id, commercial_policy_version_id)
                    REFERENCES commercial.commercial_policy_versions (tenant_id, id),
                CONSTRAINT fk_booking_inventory_supplier FOREIGN KEY (
                    supplier_tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_booking_inventory_product FOREIGN KEY (
                    supplier_tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_booking_product_version FOREIGN KEY (
                    supplier_tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_booking_rate FOREIGN KEY (supplier_tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_booking_availability FOREIGN KEY (
                    supplier_tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                CONSTRAINT fk_booking_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_booking_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_booking_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_booking_requester FOREIGN KEY (requested_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_booking_confirmer FOREIGN KEY (confirmed_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_booking_buyer_status
                ON commercial.bookings (buyer_tenant_id, status_code, updated_at_utc DESC);
            CREATE INDEX ix_booking_supplier_status
                ON commercial.bookings (supplier_tenant_id, status_code, updated_at_utc DESC);
            """);
}
