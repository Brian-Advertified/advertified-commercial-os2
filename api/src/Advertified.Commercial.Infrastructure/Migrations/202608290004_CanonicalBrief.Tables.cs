using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalBrief
{
    private static void CreateBriefTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.campaign_briefs (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                client_account_id uuid NOT NULL,
                opportunity_id uuid,
                title varchar(300) NOT NULL,
                owner_user_id uuid NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                current_draft_version_id uuid,
                approved_version_id uuid,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_campaign_briefs_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_campaign_briefs_version CHECK (version > 0),
                CONSTRAINT ck_campaign_briefs_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_campaign_briefs_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_campaign_briefs_client FOREIGN KEY (tenant_id, client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id),
                CONSTRAINT fk_campaign_briefs_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_campaign_briefs_owner FOREIGN KEY (owner_user_id)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_campaign_briefs_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE UNIQUE INDEX ux_campaign_briefs_opportunity
                ON commercial.campaign_briefs (tenant_id, opportunity_id)
                WHERE opportunity_id IS NOT NULL;

            CREATE TABLE commercial.brief_sources (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                brief_id uuid NOT NULL,
                source_type_collection_code varchar(100) NOT NULL DEFAULT 'briefSourceTypes',
                source_type_code varchar(100) NOT NULL,
                locator varchar(2048) NOT NULL,
                title varchar(300) NOT NULL,
                content text NOT NULL,
                content_hash varchar(64) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_brief_sources_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_brief_sources_content UNIQUE (tenant_id, brief_id, content_hash),
                CONSTRAINT ck_brief_sources_type_collection
                    CHECK (source_type_collection_code = 'briefSourceTypes'),
                CONSTRAINT ck_brief_sources_hash
                    CHECK (content_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_brief_sources_brief FOREIGN KEY (tenant_id, brief_id)
                    REFERENCES commercial.campaign_briefs (tenant_id, id),
                CONSTRAINT fk_brief_sources_type
                    FOREIGN KEY (source_type_collection_code, source_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_sources_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.brief_versions (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                brief_id uuid NOT NULL,
                base_version_id uuid,
                source_id uuid NOT NULL,
                version_no integer NOT NULL,
                business_problem text NOT NULL,
                objective text NOT NULL,
                audiences_json jsonb NOT NULL,
                geographies_json jsonb NOT NULL,
                timing text NOT NULL,
                budget_minor bigint,
                budget_unknown boolean NOT NULL,
                currency_collection_code varchar(100) DEFAULT 'currencies',
                currency_code varchar(100),
                vat_collection_code varchar(100) DEFAULT 'vatStatuses',
                vat_status_code varchar(100),
                fees_minor bigint,
                constraints_json jsonb NOT NULL,
                measurement_json jsonb NOT NULL,
                facts_json jsonb NOT NULL,
                unknowns_json jsonb NOT NULL,
                assumptions_json jsonb NOT NULL,
                conflicts_json jsonb NOT NULL,
                evidence_bindings_json jsonb NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                submitted_by uuid,
                submitted_at_utc timestamptz,
                approved_by uuid,
                approved_at_utc timestamptz,
                rejected_by uuid,
                rejected_at_utc timestamptz,
                rejection_reason varchar(1000),
                requested_changes varchar(2000),
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_brief_versions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_brief_versions_brief_version UNIQUE (tenant_id, brief_id, version_no),
                CONSTRAINT ck_brief_versions_numbers CHECK (
                    version > 0 AND version_no > 0 AND
                    (budget_minor IS NULL OR budget_minor >= 0) AND
                    (fees_minor IS NULL OR fees_minor >= 0)),
                CONSTRAINT ck_brief_versions_budget CHECK (
                    (budget_unknown AND budget_minor IS NULL AND currency_code IS NULL) OR
                    (NOT budget_unknown AND budget_minor IS NOT NULL AND currency_code IS NOT NULL)),
                CONSTRAINT ck_brief_versions_currency_collection CHECK (
                    currency_code IS NULL OR currency_collection_code = 'currencies'),
                CONSTRAINT ck_brief_versions_vat_collection CHECK (
                    vat_status_code IS NULL OR vat_collection_code = 'vatStatuses'),
                CONSTRAINT ck_brief_versions_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_brief_versions_brief FOREIGN KEY (tenant_id, brief_id)
                    REFERENCES commercial.campaign_briefs (tenant_id, id),
                CONSTRAINT fk_brief_versions_base FOREIGN KEY (tenant_id, base_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_brief_versions_source FOREIGN KEY (tenant_id, source_id)
                    REFERENCES commercial.brief_sources (tenant_id, id),
                CONSTRAINT fk_brief_versions_currency
                    FOREIGN KEY (currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_versions_vat
                    FOREIGN KEY (vat_collection_code, vat_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_versions_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_versions_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_brief_versions_submitter FOREIGN KEY (submitted_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_brief_versions_approver FOREIGN KEY (approved_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_brief_versions_rejector FOREIGN KEY (rejected_by)
                    REFERENCES commercial.users (id)
            );

            ALTER TABLE commercial.campaign_briefs
                ADD CONSTRAINT fk_campaign_briefs_current_draft
                FOREIGN KEY (tenant_id, current_draft_version_id)
                REFERENCES commercial.brief_versions (tenant_id, id);
            ALTER TABLE commercial.campaign_briefs
                ADD CONSTRAINT fk_campaign_briefs_approved_version
                FOREIGN KEY (tenant_id, approved_version_id)
                REFERENCES commercial.brief_versions (tenant_id, id);

            CREATE TABLE commercial.brief_version_evidence_items (
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                evidence_item_id uuid NOT NULL,
                PRIMARY KEY (tenant_id, brief_version_id, evidence_item_id),
                CONSTRAINT fk_brief_version_evidence_version
                    FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_brief_version_evidence_item
                    FOREIGN KEY (tenant_id, evidence_item_id)
                    REFERENCES commercial.evidence_items (tenant_id, id)
            );

            ALTER TABLE commercial.human_tasks ALTER COLUMN opportunity_id DROP NOT NULL;
            """);
    }
}
