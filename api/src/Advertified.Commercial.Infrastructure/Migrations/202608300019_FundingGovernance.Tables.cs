using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class FundingGovernance
{
    private static void CreateFundingTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.purchase_orders (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                proposal_option_id uuid NOT NULL,
                proposal_decision_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                po_number varchar(100) NOT NULL,
                object_key varchar(1000) NOT NULL,
                content_sha256 char(64) NOT NULL,
                media_type varchar(100) NOT NULL,
                size_bytes bigint NOT NULL,
                amount_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                submitted_by uuid NOT NULL,
                submitted_at_utc timestamptz NOT NULL,
                approved_by uuid,
                approved_at_utc timestamptz,
                reconciliation_reason varchar(1000),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_purchase_order_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_purchase_order_decision UNIQUE (tenant_id, proposal_decision_id),
                CONSTRAINT ux_purchase_order_number UNIQUE (tenant_id, po_number),
                CONSTRAINT ck_purchase_order_numbers CHECK (
                    amount_minor >= 0 AND size_bytes > 0 AND version > 0),
                CONSTRAINT ck_purchase_order_hash CHECK (content_sha256 ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_purchase_order_collections CHECK (
                    currency_collection_code = 'currencies'
                    AND status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_purchase_order_status_shape CHECK (
                    (status_code = 'SUBMITTED' AND approved_by IS NULL
                        AND approved_at_utc IS NULL AND reconciliation_reason IS NULL)
                    OR (status_code = 'APPROVED' AND approved_by IS NOT NULL
                        AND approved_at_utc IS NOT NULL
                        AND btrim(COALESCE(reconciliation_reason, '')) <> '')),
                CONSTRAINT fk_purchase_order_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_purchase_order_proposal FOREIGN KEY (tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_purchase_order_option FOREIGN KEY (tenant_id, proposal_option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_purchase_order_decision FOREIGN KEY (tenant_id, proposal_decision_id)
                    REFERENCES commercial.proposal_decisions (tenant_id, id),
                CONSTRAINT fk_purchase_order_plan FOREIGN KEY (tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_purchase_order_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_purchase_order_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_purchase_order_submitter FOREIGN KEY (submitted_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_purchase_order_approver FOREIGN KEY (approved_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.invoices (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                proposal_option_id uuid NOT NULL,
                purchase_order_id uuid NOT NULL,
                invoice_number varchar(100) NOT NULL,
                subtotal_minor bigint NOT NULL,
                fees_minor bigint NOT NULL,
                vat_minor bigint NOT NULL,
                total_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                issued_by uuid NOT NULL,
                issued_at_utc timestamptz NOT NULL,
                version bigint NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_invoice_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_invoice_purchase_order UNIQUE (tenant_id, purchase_order_id),
                CONSTRAINT ux_invoice_number UNIQUE (tenant_id, invoice_number),
                CONSTRAINT ck_invoice_numbers CHECK (
                    subtotal_minor >= 0 AND fees_minor >= 0 AND vat_minor >= 0
                    AND total_minor = subtotal_minor + fees_minor + vat_minor AND version = 1),
                CONSTRAINT ck_invoice_shape CHECK (
                    currency_collection_code = 'currencies'
                    AND status_collection_code = 'lifecycleStatuses' AND status_code = 'ISSUED'),
                CONSTRAINT fk_invoice_proposal FOREIGN KEY (tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_invoice_option FOREIGN KEY (tenant_id, proposal_option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_invoice_purchase_order FOREIGN KEY (tenant_id, purchase_order_id)
                    REFERENCES commercial.purchase_orders (tenant_id, id),
                CONSTRAINT fk_invoice_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_invoice_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_invoice_issuer FOREIGN KEY (issued_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.payment_intents (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                proposal_option_id uuid NOT NULL,
                purchase_order_id uuid NOT NULL,
                invoice_id uuid NOT NULL,
                method_collection_code varchar(100) NOT NULL DEFAULT 'paymentMethods',
                method_code varchar(100) NOT NULL,
                amount_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                started_by uuid NOT NULL,
                started_at_utc timestamptz NOT NULL,
                reconciled_by uuid,
                reconciled_at_utc timestamptz,
                reconciliation_reference varchar(300),
                reconciliation_reason varchar(1000),
                receipt_object_key varchar(1000),
                receipt_sha256 char(64),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_payment_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_payment_invoice UNIQUE (tenant_id, invoice_id),
                CONSTRAINT ck_payment_numbers CHECK (amount_minor >= 0 AND version > 0),
                CONSTRAINT ck_payment_collections CHECK (
                    method_collection_code = 'paymentMethods'
                    AND currency_collection_code = 'currencies'
                    AND status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_payment_status_shape CHECK (
                    (status_code = 'PENDING' AND reconciled_by IS NULL
                        AND reconciled_at_utc IS NULL AND reconciliation_reference IS NULL
                        AND reconciliation_reason IS NULL AND receipt_object_key IS NULL
                        AND receipt_sha256 IS NULL)
                    OR (status_code = 'CONFIRMED' AND reconciled_by IS NOT NULL
                        AND reconciled_at_utc IS NOT NULL
                        AND btrim(COALESCE(reconciliation_reference, '')) <> ''
                        AND btrim(COALESCE(reconciliation_reason, '')) <> ''
                        AND btrim(COALESCE(receipt_object_key, '')) <> ''
                        AND receipt_sha256 ~ '^[0-9a-f]{64}$')),
                CONSTRAINT fk_payment_proposal FOREIGN KEY (tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_payment_option FOREIGN KEY (tenant_id, proposal_option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_payment_purchase_order FOREIGN KEY (tenant_id, purchase_order_id)
                    REFERENCES commercial.purchase_orders (tenant_id, id),
                CONSTRAINT fk_payment_invoice FOREIGN KEY (tenant_id, invoice_id)
                    REFERENCES commercial.invoices (tenant_id, id),
                CONSTRAINT fk_payment_method FOREIGN KEY (method_collection_code, method_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_payment_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_payment_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_payment_starter FOREIGN KEY (started_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_payment_reconciler FOREIGN KEY (reconciled_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_purchase_orders_status
                ON commercial.purchase_orders (tenant_id, status_code, updated_at_utc DESC);
            CREATE INDEX ix_invoices_issued
                ON commercial.invoices (tenant_id, issued_at_utc DESC);
            CREATE INDEX ix_payments_status
                ON commercial.payment_intents (tenant_id, status_code, updated_at_utc DESC);
            """);
}
