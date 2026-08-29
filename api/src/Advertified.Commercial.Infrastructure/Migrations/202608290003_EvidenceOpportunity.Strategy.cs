using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class EvidenceOpportunity
{
    private static void CreateStrategyTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.business_interpretations (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                agent_run_id uuid NOT NULL,
                evidence_set_id uuid NOT NULL,
                version_no integer NOT NULL,
                artifact_json jsonb NOT NULL,
                evidence_bindings_json jsonb NOT NULL,
                unknowns_json jsonb NOT NULL,
                assumptions_json jsonb NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                confirmed_by uuid,
                confirmed_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_interpretations_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_interpretations_opportunity_version UNIQUE (tenant_id, opportunity_id, version_no),
                CONSTRAINT ck_interpretations_version CHECK (version > 0 AND version_no > 0),
                CONSTRAINT ck_interpretations_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_interpretations_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_interpretations_run FOREIGN KEY (tenant_id, agent_run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_interpretations_evidence FOREIGN KEY (tenant_id, evidence_set_id)
                    REFERENCES commercial.evidence_sets (tenant_id, id),
                CONSTRAINT fk_interpretations_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_interpretations_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_interpretations_confirmer FOREIGN KEY (confirmed_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.opportunity_angle_sets (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                agent_run_id uuid NOT NULL,
                interpretation_id uuid NOT NULL,
                version_no integer NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_angle_sets_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_angle_sets_opportunity_version UNIQUE (tenant_id, opportunity_id, version_no),
                CONSTRAINT ck_angle_sets_version CHECK (version > 0 AND version_no > 0),
                CONSTRAINT ck_angle_sets_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_angle_sets_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_angle_sets_run FOREIGN KEY (tenant_id, agent_run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_angle_sets_interpretation FOREIGN KEY (tenant_id, interpretation_id)
                    REFERENCES commercial.business_interpretations (tenant_id, id),
                CONSTRAINT fk_angle_sets_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_angle_sets_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.opportunity_angles (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                angle_set_id uuid NOT NULL,
                rank integer NOT NULL,
                title varchar(300) NOT NULL,
                rationale varchar(2000) NOT NULL,
                evidence_item_ids_json jsonb NOT NULL,
                confidence numeric(5,4) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'opportunityAngleStatuses',
                status_code varchar(100) NOT NULL,
                selected_by uuid,
                selected_at_utc timestamptz,
                version bigint NOT NULL,
                CONSTRAINT ux_angles_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_angles_set_rank UNIQUE (tenant_id, angle_set_id, rank),
                CONSTRAINT ck_angles_rank CHECK (rank > 0),
                CONSTRAINT ck_angles_confidence CHECK (confidence >= 0 AND confidence <= 1),
                CONSTRAINT ck_angles_version CHECK (version > 0),
                CONSTRAINT ck_angles_status_collection CHECK (status_collection_code = 'opportunityAngleStatuses'),
                CONSTRAINT fk_angles_set FOREIGN KEY (tenant_id, angle_set_id)
                    REFERENCES commercial.opportunity_angle_sets (tenant_id, id),
                CONSTRAINT fk_angles_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_angles_selector FOREIGN KEY (selected_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.strategy_versions (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                agent_run_id uuid NOT NULL,
                evidence_set_id uuid NOT NULL,
                interpretation_id uuid NOT NULL,
                selected_angle_id uuid NOT NULL,
                version_no integer NOT NULL,
                artifact_json jsonb NOT NULL,
                evidence_bindings_json jsonb NOT NULL,
                unknowns_json jsonb NOT NULL,
                assumptions_json jsonb NOT NULL,
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
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_strategies_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_strategies_opportunity_version UNIQUE (tenant_id, opportunity_id, version_no),
                CONSTRAINT ck_strategies_version CHECK (version > 0 AND version_no > 0),
                CONSTRAINT ck_strategies_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_strategies_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_strategies_run FOREIGN KEY (tenant_id, agent_run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_strategies_evidence FOREIGN KEY (tenant_id, evidence_set_id)
                    REFERENCES commercial.evidence_sets (tenant_id, id),
                CONSTRAINT fk_strategies_interpretation FOREIGN KEY (tenant_id, interpretation_id)
                    REFERENCES commercial.business_interpretations (tenant_id, id),
                CONSTRAINT fk_strategies_angle FOREIGN KEY (tenant_id, selected_angle_id)
                    REFERENCES commercial.opportunity_angles (tenant_id, id),
                CONSTRAINT fk_strategies_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_strategies_creator FOREIGN KEY (created_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_strategies_submitter FOREIGN KEY (submitted_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_strategies_approver FOREIGN KEY (approved_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_strategies_rejector FOREIGN KEY (rejected_by) REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.critic_reports (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                agent_run_id uuid NOT NULL,
                strategy_version_id uuid NOT NULL,
                artifact_json jsonb NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_critic_reports_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_critic_reports_strategy UNIQUE (tenant_id, strategy_version_id),
                CONSTRAINT fk_critic_reports_run FOREIGN KEY (tenant_id, agent_run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_critic_reports_strategy FOREIGN KEY (tenant_id, strategy_version_id)
                    REFERENCES commercial.strategy_versions (tenant_id, id)
            );

            CREATE TABLE commercial.critic_objections (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                critic_report_id uuid NOT NULL,
                severity_collection_code varchar(100) NOT NULL DEFAULT 'criticSeverities',
                severity_code varchar(100) NOT NULL,
                field_path varchar(200) NOT NULL,
                evidence_gap varchar(1000) NOT NULL,
                recommended_resolution varchar(1000) NOT NULL,
                resolution_collection_code varchar(100),
                resolution_code varchar(100),
                resolution_reason varchar(1000),
                resolved_by uuid,
                resolved_at_utc timestamptz,
                version bigint NOT NULL,
                CONSTRAINT ux_critic_objections_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_critic_objections_version CHECK (version > 0),
                CONSTRAINT ck_critic_severity_collection CHECK (severity_collection_code = 'criticSeverities'),
                CONSTRAINT ck_critic_resolution_collection CHECK (
                    resolution_collection_code IS NULL OR resolution_collection_code = 'objectionResolutions'),
                CONSTRAINT fk_critic_objections_report FOREIGN KEY (tenant_id, critic_report_id)
                    REFERENCES commercial.critic_reports (tenant_id, id),
                CONSTRAINT fk_critic_objections_severity FOREIGN KEY (severity_collection_code, severity_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_critic_objections_resolution FOREIGN KEY (resolution_collection_code, resolution_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_critic_objections_resolver FOREIGN KEY (resolved_by) REFERENCES commercial.users (id)
            );
            """);
    }
}
