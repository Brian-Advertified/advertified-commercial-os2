namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class ManualPartnerFunding
{
    private static string PaymentTransitionSql(bool includeManualPartner) => """
        CREATE OR REPLACE FUNCTION commercial.enforce_payment_transition() RETURNS trigger
            LANGUAGE plpgsql
            AS $$
        DECLARE expected record;
        BEGIN
            IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'payments cannot be deleted'; END IF;
            IF TG_OP = 'INSERT' THEN
                SELECT status_code, proposal_version_id, proposal_option_id,
                    purchase_order_id, total_minor, currency_code INTO expected
                FROM commercial.invoices
                WHERE tenant_id = NEW.tenant_id AND id = NEW.invoice_id;
                IF NOT FOUND OR expected.status_code <> 'ISSUED'
                   OR NEW.status_code <> 'PENDING'
                   OR NEW.started_by <> commercial.current_user_id()
                   OR NEW.method_code METHOD_GUARD
                   OR (NEW.proposal_version_id, NEW.proposal_option_id,
                       NEW.purchase_order_id, NEW.amount_minor, NEW.currency_code)
                      IS DISTINCT FROM
                      (expected.proposal_version_id, expected.proposal_option_id,
                       expected.purchase_order_id, expected.total_minor, expected.currency_code) THEN
                    RAISE EXCEPTION 'payment does not reconcile to issued invoice';
                END IF;
                RETURN NEW;
            END IF;
            IF (NEW.id, NEW.tenant_id, NEW.proposal_version_id, NEW.proposal_option_id,
                NEW.purchase_order_id, NEW.invoice_id, NEW.method_collection_code,
                NEW.method_code, NEW.amount_minor, NEW.currency_collection_code,
                NEW.currency_code, NEW.started_by, NEW.started_at_utc) IS DISTINCT FROM
               (OLD.id, OLD.tenant_id, OLD.proposal_version_id, OLD.proposal_option_id,
                OLD.purchase_order_id, OLD.invoice_id, OLD.method_collection_code,
                OLD.method_code, OLD.amount_minor, OLD.currency_collection_code,
                OLD.currency_code, OLD.started_by, OLD.started_at_utc) THEN
                RAISE EXCEPTION 'payment commercial snapshot is immutable';
            END IF;
            IF OLD.status_code <> 'PENDING' OR NEW.status_code <> 'CONFIRMED'
               OR NEW.reconciled_by <> commercial.current_user_id()
               OR NEW.reconciled_by = OLD.started_by
               OR NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                RAISE EXCEPTION 'invalid payment reconciliation';
            END IF;
            RETURN NEW;
        END;
        $$;
        """.Replace("METHOD_GUARD", includeManualPartner
            ? "NOT IN ('MANUAL_EFT', 'ADVERTISE_NOW_PAY_LATER')" : "<> 'MANUAL_EFT'",
            StringComparison.Ordinal);
}
