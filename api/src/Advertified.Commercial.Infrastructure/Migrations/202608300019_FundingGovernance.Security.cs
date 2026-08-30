using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class FundingGovernance
{
    private static void CreateFundingSecurityBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.purchase_orders ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.purchase_orders FORCE ROW LEVEL SECURITY;
            CREATE POLICY purchase_orders_tenant_scope ON commercial.purchase_orders
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.invoices ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.invoices FORCE ROW LEVEL SECURITY;
            CREATE POLICY invoices_tenant_scope ON commercial.invoices
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.payment_intents ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.payment_intents FORCE ROW LEVEL SECURITY;
            CREATE POLICY payment_intents_tenant_scope ON commercial.payment_intents
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.enforce_purchase_order_transition()
            RETURNS trigger LANGUAGE plpgsql AS $purchase_order_transition$
            DECLARE expected record;
            BEGIN
                IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'purchase orders cannot be deleted'; END IF;
                IF TG_OP = 'INSERT' THEN
                    SELECT proposal.status_code AS proposal_status,
                        decision.decision_code, decision.option_id,
                        option.plan_version_id, option.budget_minor,
                        option.currency_code, plan.status_code AS plan_status,
                        plan.total_minor INTO expected
                    FROM commercial.proposal_versions proposal
                    JOIN commercial.proposal_decisions decision
                      ON decision.tenant_id = proposal.tenant_id
                     AND decision.proposal_version_id = proposal.id
                    JOIN commercial.proposal_options option
                      ON option.tenant_id = decision.tenant_id AND option.id = decision.option_id
                    JOIN commercial.media_plan_versions plan
                      ON plan.tenant_id = option.tenant_id AND plan.id = option.plan_version_id
                    WHERE proposal.tenant_id = NEW.tenant_id
                      AND proposal.id = NEW.proposal_version_id
                      AND option.id = NEW.proposal_option_id
                      AND decision.id = NEW.proposal_decision_id;
                    IF NOT FOUND OR expected.proposal_status <> 'SELECTED'
                       OR expected.decision_code <> 'SELECTED'
                       OR expected.plan_status <> 'APPROVED'
                       OR (NEW.proposal_option_id, NEW.plan_version_id,
                           NEW.amount_minor, NEW.currency_code)
                          IS DISTINCT FROM
                          (expected.option_id, expected.plan_version_id,
                           expected.total_minor, expected.currency_code)
                       OR expected.budget_minor <> expected.total_minor
                       OR NEW.status_code <> 'SUBMITTED'
                       OR NEW.submitted_by <> commercial.current_user_id() THEN
                        RAISE EXCEPTION 'purchase order does not reconcile to selected option';
                    END IF;
                    RETURN NEW;
                END IF;
                IF (NEW.id, NEW.tenant_id, NEW.proposal_version_id, NEW.proposal_option_id,
                    NEW.proposal_decision_id, NEW.plan_version_id, NEW.po_number, NEW.object_key,
                    NEW.content_sha256, NEW.media_type, NEW.size_bytes, NEW.amount_minor,
                    NEW.currency_collection_code, NEW.currency_code, NEW.submitted_by,
                    NEW.submitted_at_utc) IS DISTINCT FROM
                   (OLD.id, OLD.tenant_id, OLD.proposal_version_id, OLD.proposal_option_id,
                    OLD.proposal_decision_id, OLD.plan_version_id, OLD.po_number, OLD.object_key,
                    OLD.content_sha256, OLD.media_type, OLD.size_bytes, OLD.amount_minor,
                    OLD.currency_collection_code, OLD.currency_code, OLD.submitted_by,
                    OLD.submitted_at_utc) THEN
                    RAISE EXCEPTION 'purchase order snapshot is immutable';
                END IF;
                IF OLD.status_code <> 'SUBMITTED' OR NEW.status_code <> 'APPROVED'
                   OR NEW.approved_by <> commercial.current_user_id()
                   OR NEW.approved_by = OLD.submitted_by
                   OR NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'invalid purchase order approval';
                END IF;
                RETURN NEW;
            END;
            $purchase_order_transition$;

            CREATE FUNCTION commercial.enforce_invoice_integrity()
            RETURNS trigger LANGUAGE plpgsql AS $invoice_integrity$
            DECLARE expected record;
            BEGIN
                IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'invoices are immutable'; END IF;
                SELECT po.status_code, po.proposal_version_id, po.proposal_option_id,
                    po.amount_minor, po.currency_code, plan.subtotal_minor,
                    plan.fees_minor, plan.vat_minor, plan.total_minor
                INTO expected
                FROM commercial.purchase_orders po
                JOIN commercial.media_plan_versions plan
                  ON plan.tenant_id = po.tenant_id AND plan.id = po.plan_version_id
                WHERE po.tenant_id = NEW.tenant_id AND po.id = NEW.purchase_order_id;
                IF NOT FOUND OR expected.status_code <> 'APPROVED'
                   OR NEW.status_code <> 'ISSUED'
                   OR NEW.issued_by <> commercial.current_user_id()
                   OR (NEW.proposal_version_id, NEW.proposal_option_id, NEW.total_minor,
                       NEW.currency_code, NEW.subtotal_minor, NEW.fees_minor, NEW.vat_minor)
                      IS DISTINCT FROM
                      (expected.proposal_version_id, expected.proposal_option_id,
                       expected.total_minor, expected.currency_code, expected.subtotal_minor,
                       expected.fees_minor, expected.vat_minor)
                   OR NEW.total_minor <> expected.amount_minor THEN
                    RAISE EXCEPTION 'invoice does not reconcile to approved purchase order';
                END IF;
                RETURN NEW;
            END;
            $invoice_integrity$;

            CREATE FUNCTION commercial.enforce_payment_transition()
            RETURNS trigger LANGUAGE plpgsql AS $payment_transition$
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
                       OR NEW.method_code <> 'MANUAL_EFT'
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
            $payment_transition$;

            CREATE TRIGGER protect_purchase_order_transition
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.purchase_orders
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_purchase_order_transition();
            CREATE TRIGGER protect_invoice_integrity
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.invoices
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_invoice_integrity();
            CREATE TRIGGER protect_payment_transition
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.payment_intents
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_payment_transition();

            GRANT SELECT, INSERT, UPDATE ON commercial.purchase_orders TO advertified_app;
            GRANT SELECT, INSERT ON commercial.invoices TO advertified_app;
            GRANT SELECT, INSERT, UPDATE ON commercial.payment_intents TO advertified_app;
            """);
}
