using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class EvidenceOpportunity
{
    private static void CreateEvidenceTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.evidence_sources (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                type_collection_code varchar(100) NOT NULL DEFAULT 'evidenceSourceTypes',
                type_code varchar(100) NOT NULL,
                locator varchar(2048) NOT NULL,
                title varchar(300) NOT NULL,
                content_hash varchar(64) NOT NULL,
                object_key varchar(1024) NOT NULL,
                content_text text NOT NULL,
                policy_collection_code varchar(100) NOT NULL DEFAULT 'evidencePolicyBases',
                policy_code varchar(100) NOT NULL,
                capture_status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                capture_status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                captured_at_utc timestamptz NOT NULL,
                version bigint NOT NULL,
                CONSTRAINT ux_evidence_sources_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_evidence_sources_tenant_hash UNIQUE (tenant_id, type_code, content_hash),
                CONSTRAINT ck_evidence_sources_hash CHECK (content_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_evidence_sources_version CHECK (version > 0),
                CONSTRAINT ck_evidence_sources_type_collection CHECK (type_collection_code = 'evidenceSourceTypes'),
                CONSTRAINT ck_evidence_sources_policy_collection CHECK (policy_collection_code = 'evidencePolicyBases'),
                CONSTRAINT ck_evidence_sources_status_collection CHECK (capture_status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_evidence_sources_type FOREIGN KEY (type_collection_code, type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_sources_policy FOREIGN KEY (policy_collection_code, policy_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_sources_status FOREIGN KEY (capture_status_collection_code, capture_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_sources_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.opportunity_evidence_sources (
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                source_id uuid NOT NULL,
                linked_by uuid NOT NULL,
                linked_at_utc timestamptz NOT NULL,
                PRIMARY KEY (tenant_id, opportunity_id, source_id),
                CONSTRAINT fk_opportunity_sources_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_opportunity_sources_source FOREIGN KEY (tenant_id, source_id)
                    REFERENCES commercial.evidence_sources (tenant_id, id),
                CONSTRAINT fk_opportunity_sources_actor FOREIGN KEY (linked_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.evidence_items (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                source_id uuid NOT NULL,
                locator varchar(500) NOT NULL,
                claim_type_collection_code varchar(100) NOT NULL DEFAULT 'evidenceClaimTypes',
                claim_type_code varchar(100) NOT NULL,
                original_value_json jsonb NOT NULL,
                reviewed_value_json jsonb,
                excerpt varchar(2000) NOT NULL,
                confidence numeric(5,4) NOT NULL,
                review_status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                review_status_code varchar(100) NOT NULL,
                decision_collection_code varchar(100),
                decision_code varchar(100),
                review_reason varchar(1000),
                created_by uuid NOT NULL,
                reviewed_by uuid,
                reviewed_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_evidence_items_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_evidence_items_confidence CHECK (confidence >= 0 AND confidence <= 1),
                CONSTRAINT ck_evidence_items_version CHECK (version > 0),
                CONSTRAINT ck_evidence_items_claim_collection CHECK (claim_type_collection_code = 'evidenceClaimTypes'),
                CONSTRAINT ck_evidence_items_status_collection CHECK (review_status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_evidence_items_decision_collection CHECK (
                    decision_collection_code IS NULL OR decision_collection_code = 'evidenceReviewDecisions'),
                CONSTRAINT fk_evidence_items_opportunity_source
                    FOREIGN KEY (tenant_id, opportunity_id, source_id)
                    REFERENCES commercial.opportunity_evidence_sources
                        (tenant_id, opportunity_id, source_id),
                CONSTRAINT fk_evidence_items_claim FOREIGN KEY (claim_type_collection_code, claim_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_items_status FOREIGN KEY (review_status_collection_code, review_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_items_decision FOREIGN KEY (decision_collection_code, decision_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_items_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_evidence_items_reviewer FOREIGN KEY (reviewed_by) REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_evidence_items_opportunity_review
                ON commercial.evidence_items (tenant_id, opportunity_id, review_status_code, id);

            CREATE TABLE commercial.evidence_sets (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                version_no integer NOT NULL,
                gaps_json jsonb NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                approved_by uuid,
                approved_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_evidence_sets_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_evidence_sets_opportunity_version UNIQUE (tenant_id, opportunity_id, version_no),
                CONSTRAINT ck_evidence_sets_version CHECK (version > 0 AND version_no > 0),
                CONSTRAINT ck_evidence_sets_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_evidence_sets_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_evidence_sets_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_evidence_sets_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_evidence_sets_approver FOREIGN KEY (approved_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.evidence_set_items (
                tenant_id uuid NOT NULL,
                evidence_set_id uuid NOT NULL,
                evidence_item_id uuid NOT NULL,
                PRIMARY KEY (tenant_id, evidence_set_id, evidence_item_id),
                CONSTRAINT fk_evidence_set_items_set FOREIGN KEY (tenant_id, evidence_set_id)
                    REFERENCES commercial.evidence_sets (tenant_id, id),
                CONSTRAINT fk_evidence_set_items_item FOREIGN KEY (tenant_id, evidence_item_id)
                    REFERENCES commercial.evidence_items (tenant_id, id)
            );
            """);
    }
}
