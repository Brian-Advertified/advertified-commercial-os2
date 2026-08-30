using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignBookingReadiness
{
    private static void CreateCampaignTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.campaigns (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                proposal_option_id uuid NOT NULL,
                proposal_decision_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                payment_intent_id uuid NOT NULL,
                title varchar(300) NOT NULL,
                start_date date NOT NULL,
                end_date date NOT NULL,
                owner_user_id uuid NOT NULL,
                measurement_plan_json jsonb NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                bookings_confirmed_by uuid,
                bookings_confirmed_at_utc timestamptz,
                booking_confirmation_reason varchar(1000),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_campaign_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_campaign_decision UNIQUE (tenant_id, proposal_decision_id),
                CONSTRAINT ux_campaign_payment UNIQUE (tenant_id, payment_intent_id),
                CONSTRAINT ck_campaign_dates CHECK (end_date >= start_date),
                CONSTRAINT ck_campaign_version CHECK (version > 0),
                CONSTRAINT ck_campaign_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_campaign_status_shape CHECK (
                    (status_code = 'PLANNED' AND bookings_confirmed_by IS NULL
                        AND bookings_confirmed_at_utc IS NULL
                        AND booking_confirmation_reason IS NULL)
                    OR (status_code = 'BOOKED' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> '')),
                CONSTRAINT fk_campaign_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_campaign_brief FOREIGN KEY (tenant_id, brief_id)
                    REFERENCES commercial.campaign_briefs (tenant_id, id),
                CONSTRAINT fk_campaign_brief_version FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_campaign_proposal FOREIGN KEY (tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_campaign_option FOREIGN KEY (tenant_id, proposal_option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_campaign_decision FOREIGN KEY (tenant_id, proposal_decision_id)
                    REFERENCES commercial.proposal_decisions (tenant_id, id),
                CONSTRAINT fk_campaign_plan FOREIGN KEY (tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_campaign_payment FOREIGN KEY (tenant_id, payment_intent_id)
                    REFERENCES commercial.payment_intents (tenant_id, id),
                CONSTRAINT fk_campaign_owner FOREIGN KEY (owner_user_id)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_campaign_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_campaign_booking_confirmer FOREIGN KEY (bookings_confirmed_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_campaign_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_campaign_status
                ON commercial.campaigns (tenant_id, status_code, updated_at_utc DESC);
            """);
}
