using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class ProposalClientDecision
{
    private static void CreateProposalTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.proposal_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                version_no integer NOT NULL,
                title varchar(300) NOT NULL,
                executive_summary text NOT NULL,
                terms text NOT NULL,
                expiry_at_utc timestamptz NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                input_hash char(64) NOT NULL,
                created_by uuid NOT NULL,
                approved_by uuid,
                approved_at_utc timestamptz,
                recipient_user_id uuid,
                shared_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_proposal_versions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_proposal_versions_number UNIQUE (tenant_id, brief_id, version_no),
                CONSTRAINT ck_proposal_version_numbers CHECK (version_no > 0 AND version > 0),
                CONSTRAINT ck_proposal_input_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_proposal_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_proposal_brief FOREIGN KEY (tenant_id, brief_id)
                    REFERENCES commercial.campaign_briefs (tenant_id, id),
                CONSTRAINT fk_proposal_brief_version FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_proposal_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_proposal_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_proposal_approver FOREIGN KEY (approved_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_proposal_recipient FOREIGN KEY (recipient_user_id)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.proposal_options (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                plan_version_id uuid NOT NULL,
                plan_version_no integer NOT NULL,
                label varchar(200) NOT NULL,
                outcome text NOT NULL,
                budget_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                display_order integer NOT NULL,
                plan_signature char(64) NOT NULL,
                channels_json jsonb NOT NULL,
                running_periods_json jsonb NOT NULL,
                inventory_json jsonb NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_proposal_option_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_proposal_option_plan UNIQUE (
                    tenant_id, proposal_version_id, plan_version_id),
                CONSTRAINT ux_proposal_option_order UNIQUE (
                    tenant_id, proposal_version_id, display_order),
                CONSTRAINT ck_proposal_option_numbers CHECK (
                    plan_version_no > 0 AND budget_minor >= 0 AND display_order > 0),
                CONSTRAINT ck_proposal_option_hash CHECK (plan_signature ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_proposal_option_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT fk_proposal_option_version FOREIGN KEY (
                    tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_proposal_option_plan FOREIGN KEY (tenant_id, plan_version_id)
                    REFERENCES commercial.media_plan_versions (tenant_id, id),
                CONSTRAINT fk_proposal_option_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.proposal_documents (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                media_type varchar(100) NOT NULL,
                file_name varchar(300) NOT NULL,
                content_hash char(64) NOT NULL,
                content bytea NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_proposal_document_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_proposal_document_version UNIQUE (tenant_id, proposal_version_id),
                CONSTRAINT ck_proposal_document_hash CHECK (content_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_proposal_document_content CHECK (octet_length(content) > 0),
                CONSTRAINT fk_proposal_document_version FOREIGN KEY (
                    tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id)
            );

            CREATE TABLE commercial.proposal_decisions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                proposal_version_id uuid NOT NULL,
                option_id uuid,
                decision_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                decision_code varchar(100) NOT NULL,
                reason text,
                decided_by uuid NOT NULL,
                decided_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_proposal_decision_version UNIQUE (tenant_id, proposal_version_id),
                CONSTRAINT ck_proposal_decision_collection CHECK (
                    decision_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_proposal_decision_shape CHECK (
                    (decision_code = 'SELECTED' AND option_id IS NOT NULL) OR
                    (decision_code = 'DECLINED' AND option_id IS NULL)),
                CONSTRAINT fk_proposal_decision_version FOREIGN KEY (
                    tenant_id, proposal_version_id)
                    REFERENCES commercial.proposal_versions (tenant_id, id),
                CONSTRAINT fk_proposal_decision_option FOREIGN KEY (tenant_id, option_id)
                    REFERENCES commercial.proposal_options (tenant_id, id),
                CONSTRAINT fk_proposal_decision_code FOREIGN KEY (
                    decision_collection_code, decision_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_proposal_decision_actor FOREIGN KEY (decided_by)
                    REFERENCES commercial.users (id)
            );
            """);
    }
}
