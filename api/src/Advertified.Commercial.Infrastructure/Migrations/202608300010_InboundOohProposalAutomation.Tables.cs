using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InboundOohProposalAutomation
{
    private static void CreateEmailAutomationTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inbound_mailboxes (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                address varchar(320) NOT NULL,
                provider_collection_code varchar(100) NOT NULL DEFAULT 'emailProviders',
                provider_code varchar(100) NOT NULL,
                owner_user_id uuid NOT NULL,
                default_client_account_id uuid,
                auto_send_enabled boolean NOT NULL,
                allowed_sender_domains_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                is_enabled boolean NOT NULL DEFAULT true,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inbound_mailbox_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inbound_mailbox_tenant UNIQUE (tenant_id),
                CONSTRAINT ux_inbound_mailbox_address UNIQUE (provider_code, address),
                CONSTRAINT ck_inbound_mailbox_provider_collection CHECK (
                    provider_collection_code = 'emailProviders'),
                CONSTRAINT ck_inbound_mailbox_address CHECK (
                    address = lower(trim(address)) AND length(address) BETWEEN 3 AND 320),
                CONSTRAINT ck_inbound_mailbox_domains CHECK (
                    jsonb_typeof(allowed_sender_domains_json) = 'array'),
                CONSTRAINT ck_inbound_mailbox_version CHECK (version > 0),
                CONSTRAINT fk_inbound_mailbox_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_inbound_mailbox_provider FOREIGN KEY (
                    provider_collection_code, provider_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inbound_mailbox_owner FOREIGN KEY (owner_user_id)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_inbound_mailbox_client FOREIGN KEY (
                    tenant_id, default_client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id)
            );

            CREATE TABLE commercial.inbound_campaign_emails (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                mailbox_id uuid NOT NULL,
                provider_event_id varchar(300) NOT NULL,
                provider_email_id varchar(300) NOT NULL,
                provider_message_id varchar(1000) NOT NULL,
                sender_email varchar(320) NOT NULL,
                sender_name varchar(300),
                reply_to_email varchar(320) NOT NULL,
                subject varchar(1000) NOT NULL,
                body_text text NOT NULL,
                source_hash char(64) NOT NULL,
                raw_metadata_json jsonb NOT NULL,
                received_at_utc timestamptz NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inbound_email_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inbound_email_event UNIQUE (
                    tenant_id, mailbox_id, provider_event_id),
                CONSTRAINT ux_inbound_email_provider_id UNIQUE (
                    tenant_id, mailbox_id, provider_email_id),
                CONSTRAINT ux_inbound_email_message_id UNIQUE (
                    tenant_id, mailbox_id, provider_message_id),
                CONSTRAINT ux_inbound_email_source_hash UNIQUE (
                    tenant_id, mailbox_id, source_hash),
                CONSTRAINT ck_inbound_email_hash CHECK (source_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inbound_email_addresses CHECK (
                    sender_email = lower(trim(sender_email)) AND
                    reply_to_email = lower(trim(reply_to_email))),
                CONSTRAINT ck_inbound_email_metadata CHECK (
                    jsonb_typeof(raw_metadata_json) = 'object'),
                CONSTRAINT fk_inbound_email_mailbox FOREIGN KEY (tenant_id, mailbox_id)
                    REFERENCES commercial.inbound_mailboxes (tenant_id, id)
            );

            CREATE TABLE commercial.inbound_email_attachments (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                inbound_email_id uuid NOT NULL,
                provider_attachment_id varchar(300) NOT NULL,
                file_name varchar(500) NOT NULL,
                media_type varchar(200) NOT NULL,
                size_bytes bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inbound_attachment_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inbound_attachment_provider UNIQUE (
                    tenant_id, inbound_email_id, provider_attachment_id),
                CONSTRAINT ck_inbound_attachment_size CHECK (size_bytes >= 0),
                CONSTRAINT fk_inbound_attachment_email FOREIGN KEY (
                    tenant_id, inbound_email_id)
                    REFERENCES commercial.inbound_campaign_emails (tenant_id, id)
            );

            CREATE TABLE commercial.email_proposal_automation_runs (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                inbound_email_id uuid NOT NULL,
                policy_version varchar(100) NOT NULL,
                campaign_mode_collection_code varchar(100) NOT NULL DEFAULT 'campaignModes',
                campaign_mode_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'emailAutomationStatuses',
                status_code varchar(100) NOT NULL,
                checkpoint_collection_code varchar(100) NOT NULL
                    DEFAULT 'emailAutomationCheckpoints',
                checkpoint_code varchar(100) NOT NULL,
                client_account_id uuid,
                brief_id uuid,
                brief_version_id uuid,
                stp_version_id uuid,
                media_mix_version_id uuid,
                shortlist_version_id uuid,
                media_plan_version_id uuid,
                proposal_version_id uuid,
                document_id uuid,
                input_hash char(64) NOT NULL,
                understanding_json jsonb,
                clarifications_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                failure_collection_code varchar(100),
                failure_code varchar(100),
                failure_message text,
                delivery_idempotency_key varchar(300),
                delivery_provider_id varchar(300),
                incremental_ai_cost_minor bigint NOT NULL DEFAULT 0,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_email_automation_run_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_email_automation_run_email UNIQUE (tenant_id, inbound_email_id),
                CONSTRAINT ux_email_automation_delivery UNIQUE (
                    tenant_id, delivery_idempotency_key),
                CONSTRAINT ck_email_automation_hash CHECK (
                    input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_email_automation_understanding CHECK (
                    understanding_json IS NULL OR
                    jsonb_typeof(understanding_json) = 'object'),
                CONSTRAINT ck_email_automation_clarifications CHECK (
                    jsonb_typeof(clarifications_json) = 'array'),
                CONSTRAINT ck_email_automation_mode_collection CHECK (
                    campaign_mode_collection_code = 'campaignModes'),
                CONSTRAINT ck_email_automation_status_collection CHECK (
                    status_collection_code = 'emailAutomationStatuses'),
                CONSTRAINT ck_email_automation_checkpoint_collection CHECK (
                    checkpoint_collection_code = 'emailAutomationCheckpoints'),
                CONSTRAINT ck_email_automation_failure_shape CHECK (
                    (failure_code IS NULL AND failure_collection_code IS NULL) OR
                    (failure_code IS NOT NULL AND
                     failure_collection_code = 'automationFailureReasons')),
                CONSTRAINT ck_email_automation_numbers CHECK (
                    incremental_ai_cost_minor >= 0 AND version > 0),
                CONSTRAINT fk_email_automation_email FOREIGN KEY (
                    tenant_id, inbound_email_id)
                    REFERENCES commercial.inbound_campaign_emails (tenant_id, id),
                CONSTRAINT fk_email_automation_mode FOREIGN KEY (
                    campaign_mode_collection_code, campaign_mode_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_email_automation_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_email_automation_checkpoint FOREIGN KEY (
                    checkpoint_collection_code, checkpoint_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_email_automation_failure FOREIGN KEY (
                    failure_collection_code, failure_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_email_automation_client FOREIGN KEY (
                    tenant_id, client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id),
                CONSTRAINT fk_email_automation_brief FOREIGN KEY (tenant_id, brief_id)
                    REFERENCES commercial.campaign_briefs (tenant_id, id),
                CONSTRAINT fk_email_automation_brief_version FOREIGN KEY (
                    tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_email_automation_stp FOREIGN KEY (tenant_id, stp_version_id)
                    REFERENCES commercial.audience_definition_sets (tenant_id, id),
                CONSTRAINT fk_email_automation_mix FOREIGN KEY (
                    tenant_id, media_mix_version_id)
                    REFERENCES commercial.media_mix_versions (tenant_id, id),
                CONSTRAINT fk_email_automation_shortlist FOREIGN KEY (
                    tenant_id, shortlist_version_id)
                    REFERENCES commercial.inventory_shortlist_versions (tenant_id, id),
                CONSTRAINT fk_email_automation_plan FOREIGN KEY (
                    tenant_id, media_plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_email_automation_proposal FOREIGN KEY (
                    tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_email_automation_document FOREIGN KEY (
                    tenant_id, document_id)
                    REFERENCES commercial.proposal_documents (tenant_id, id)
            );

            CREATE INDEX ix_inbound_email_received
                ON commercial.inbound_campaign_emails (
                    tenant_id, received_at_utc DESC, id DESC);
            CREATE INDEX ix_email_automation_status
                ON commercial.email_proposal_automation_runs (
                    tenant_id, status_code, updated_at_utc, id);
            """);
    }
}
