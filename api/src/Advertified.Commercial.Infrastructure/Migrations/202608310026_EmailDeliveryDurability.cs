using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608310026_EmailDeliveryDurability")]
public sealed class EmailDeliveryDurability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        ALTER TABLE commercial.email_proposal_automation_runs
            ADD COLUMN delivery_provider_collection_code varchar(100),
            ADD COLUMN delivery_provider_code varchar(100),
            ADD COLUMN delivery_requested_at_utc timestamptz,
            ADD COLUMN delivery_accepted_at_utc timestamptz,
            ADD CONSTRAINT ck_email_automation_delivery_provider_shape CHECK (
                (delivery_provider_collection_code IS NULL
                    AND delivery_provider_code IS NULL)
                OR (delivery_provider_collection_code = 'emailProviders'
                    AND delivery_provider_code IS NOT NULL
                    AND btrim(delivery_provider_code) <> '')),
            ADD CONSTRAINT ck_email_automation_delivery_intent CHECK (
                delivery_requested_at_utc IS NULL
                OR (delivery_provider_collection_code = 'emailProviders'
                    AND delivery_provider_code IS NOT NULL
                    AND delivery_idempotency_key IS NOT NULL
                    AND btrim(delivery_idempotency_key) <> '')),
            ADD CONSTRAINT ck_email_automation_delivery_acceptance CHECK (
                (delivery_accepted_at_utc IS NULL
                    OR (delivery_requested_at_utc IS NOT NULL
                        AND delivery_provider_id IS NOT NULL
                        AND btrim(delivery_provider_id) <> ''))
                AND (delivery_requested_at_utc IS NULL
                    OR delivery_provider_id IS NULL
                    OR delivery_accepted_at_utc IS NOT NULL)),
            ADD CONSTRAINT ck_email_automation_delivery_checkpoint CHECK (
                (checkpoint_code <> 'DELIVERY_REQUESTED'
                    OR (delivery_requested_at_utc IS NOT NULL
                        AND delivery_accepted_at_utc IS NULL))
                AND (checkpoint_code <> 'DELIVERY_ACCEPTED'
                    OR delivery_accepted_at_utc IS NOT NULL)
                AND (delivery_requested_at_utc IS NULL
                    OR checkpoint_code IN (
                        'DELIVERY_REQUESTED', 'DELIVERY_ACCEPTED', 'SENT'))
                AND (delivery_accepted_at_utc IS NULL
                    OR checkpoint_code IN ('DELIVERY_ACCEPTED', 'SENT'))
                AND (checkpoint_code <> 'SENT'
                    OR delivery_requested_at_utc IS NULL
                    OR delivery_accepted_at_utc IS NOT NULL)),
            ADD CONSTRAINT fk_email_automation_delivery_provider FOREIGN KEY (
                delivery_provider_collection_code, delivery_provider_code)
                REFERENCES governance.master_data_items (collection_code, code);

        CREATE FUNCTION commercial.enforce_email_delivery_evidence()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $email_delivery_evidence$
        BEGIN
            IF OLD.delivery_provider_collection_code IS NOT NULL
               AND (NEW.delivery_provider_collection_code IS DISTINCT FROM
                        OLD.delivery_provider_collection_code
                    OR NEW.delivery_provider_code IS DISTINCT FROM
                        OLD.delivery_provider_code) THEN
                RAISE EXCEPTION 'email delivery provider is immutable';
            END IF;
            IF OLD.delivery_idempotency_key IS NOT NULL
               AND NEW.delivery_idempotency_key IS DISTINCT FROM
                    OLD.delivery_idempotency_key THEN
                RAISE EXCEPTION 'email delivery idempotency key is immutable';
            END IF;
            IF OLD.delivery_requested_at_utc IS NOT NULL
               AND NEW.delivery_requested_at_utc IS DISTINCT FROM
                    OLD.delivery_requested_at_utc THEN
                RAISE EXCEPTION 'email delivery request evidence is immutable';
            END IF;
            IF OLD.delivery_accepted_at_utc IS NOT NULL
               AND (NEW.delivery_accepted_at_utc IS DISTINCT FROM
                        OLD.delivery_accepted_at_utc
                    OR NEW.delivery_provider_id IS DISTINCT FROM
                        OLD.delivery_provider_id) THEN
                RAISE EXCEPTION 'email delivery acceptance evidence is immutable';
            END IF;
            RETURN NEW;
        END;
        $email_delivery_evidence$;

        REVOKE ALL ON FUNCTION commercial.enforce_email_delivery_evidence() FROM PUBLIC;
        CREATE TRIGGER protect_email_delivery_evidence
            BEFORE UPDATE ON commercial.email_proposal_automation_runs
            FOR EACH ROW EXECUTE FUNCTION commercial.enforce_email_delivery_evidence();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        ALTER TABLE commercial.email_proposal_automation_runs DISABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.audit_events DISABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.outbox_messages DISABLE ROW LEVEL SECURITY;

        DO $email_delivery_rollback_guard$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM commercial.email_proposal_automation_runs
                WHERE delivery_provider_collection_code IS NOT NULL
                   OR delivery_provider_code IS NOT NULL
                   OR delivery_requested_at_utc IS NOT NULL
                   OR delivery_accepted_at_utc IS NOT NULL
                   OR checkpoint_code IN ('DELIVERY_REQUESTED', 'DELIVERY_ACCEPTED'))
               OR EXISTS (
                   SELECT 1
                   FROM commercial.audit_events
                   WHERE action_code IN (
                       'email_automation.delivery_requested',
                       'email_automation.delivery_accepted'))
               OR EXISTS (
                   SELECT 1
                   FROM commercial.outbox_messages
                   WHERE event_type_code IN (
                       'EmailProposalDeliveryRequested',
                       'EmailProposalDeliveryAccepted')) THEN
                RAISE EXCEPTION
                    'email delivery durability migration cannot roll back while delivery evidence exists';
            END IF;
        END;
        $email_delivery_rollback_guard$;

        ALTER TABLE commercial.email_proposal_automation_runs ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.email_proposal_automation_runs FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.audit_events ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.audit_events FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.outbox_messages ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.outbox_messages FORCE ROW LEVEL SECURITY;

        DROP TRIGGER protect_email_delivery_evidence
            ON commercial.email_proposal_automation_runs;
        DROP FUNCTION commercial.enforce_email_delivery_evidence();

        ALTER TABLE commercial.email_proposal_automation_runs
            DROP CONSTRAINT fk_email_automation_delivery_provider,
            DROP CONSTRAINT ck_email_automation_delivery_checkpoint,
            DROP CONSTRAINT ck_email_automation_delivery_acceptance,
            DROP CONSTRAINT ck_email_automation_delivery_intent,
            DROP CONSTRAINT ck_email_automation_delivery_provider_shape,
            DROP COLUMN delivery_accepted_at_utc,
            DROP COLUMN delivery_requested_at_utc,
            DROP COLUMN delivery_provider_code,
            DROP COLUMN delivery_provider_collection_code;
        """);
}
