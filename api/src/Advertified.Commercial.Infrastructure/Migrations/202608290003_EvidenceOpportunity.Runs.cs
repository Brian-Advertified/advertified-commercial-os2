using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class EvidenceOpportunity
{
    private static void CreateRunTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.agent_runs (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                run_kind_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                input_version bigint NOT NULL,
                requested_by uuid NOT NULL,
                approver_user_id uuid,
                correlation_id uuid NOT NULL,
                current_step_code varchar(100),
                attempts integer NOT NULL DEFAULT 0,
                next_attempt_at_utc timestamptz,
                lease_owner uuid,
                lease_expires_at_utc timestamptz,
                error_code varchar(100),
                error_detail varchar(1000),
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                completed_at_utc timestamptz,
                CONSTRAINT ux_agent_runs_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_agent_runs_version CHECK (version > 0 AND input_version > 0),
                CONSTRAINT ck_agent_runs_attempts CHECK (attempts >= 0),
                CONSTRAINT ck_agent_runs_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_agent_runs_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_agent_runs_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_agent_runs_requester FOREIGN KEY (requested_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_agent_runs_approver FOREIGN KEY (approver_user_id) REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_agent_runs_claim
                ON commercial.agent_runs (status_code, next_attempt_at_utc, lease_expires_at_utc, created_at_utc);

            CREATE TABLE commercial.agent_run_steps (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                run_id uuid NOT NULL,
                step_code varchar(100) NOT NULL,
                agent_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                input_hash varchar(64) NOT NULL,
                output_json jsonb,
                attempt_count integer NOT NULL,
                checkpointed_at_utc timestamptz,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_agent_run_steps_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_agent_run_steps_code UNIQUE (tenant_id, run_id, step_code),
                CONSTRAINT ck_agent_run_steps_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_agent_run_steps_attempts CHECK (attempt_count >= 0),
                CONSTRAINT ck_agent_run_steps_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_agent_run_steps_run FOREIGN KEY (tenant_id, run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_agent_run_steps_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.ai_usage_ledger (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                run_id uuid NOT NULL,
                step_id uuid NOT NULL,
                provider_code varchar(100) NOT NULL,
                model_code varchar(100) NOT NULL,
                units integer NOT NULL,
                tool_calls integer NOT NULL,
                incremental_cost_minor bigint NOT NULL,
                cache_status_code varchar(100) NOT NULL,
                recorded_at_utc timestamptz NOT NULL,
                CONSTRAINT ck_ai_usage_nonnegative CHECK (
                    units >= 0 AND tool_calls >= 0 AND incremental_cost_minor >= 0),
                CONSTRAINT ck_ai_usage_gate4_cost CHECK (incremental_cost_minor = 0),
                CONSTRAINT fk_ai_usage_run FOREIGN KEY (tenant_id, run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_ai_usage_step FOREIGN KEY (tenant_id, step_id)
                    REFERENCES commercial.agent_run_steps (tenant_id, id)
            );

            CREATE TABLE commercial.human_tasks (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                opportunity_id uuid NOT NULL,
                task_type_collection_code varchar(100) NOT NULL DEFAULT 'humanTaskTypes',
                task_type_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                title varchar(300) NOT NULL,
                why_it_matters varchar(1000) NOT NULL,
                resource_type_code varchar(100) NOT NULL,
                resource_id uuid NOT NULL,
                resource_version bigint NOT NULL,
                assignee_user_id uuid NOT NULL,
                action_schema_json jsonb NOT NULL,
                due_at_utc timestamptz,
                completed_by uuid,
                completed_at_utc timestamptz,
                completion_json jsonb,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_human_tasks_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_human_tasks_version CHECK (version > 0 AND resource_version > 0),
                CONSTRAINT ck_human_tasks_type_collection CHECK (task_type_collection_code = 'humanTaskTypes'),
                CONSTRAINT ck_human_tasks_status_collection CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_human_tasks_opportunity FOREIGN KEY (tenant_id, opportunity_id)
                    REFERENCES commercial.opportunities (tenant_id, id),
                CONSTRAINT fk_human_tasks_type FOREIGN KEY (task_type_collection_code, task_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_human_tasks_status FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_human_tasks_assignee FOREIGN KEY (assignee_user_id) REFERENCES commercial.users (id),
                CONSTRAINT fk_human_tasks_completer FOREIGN KEY (completed_by) REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_human_tasks_assignee_status
                ON commercial.human_tasks (tenant_id, assignee_user_id, status_code, created_at_utc);
            """);
    }
}
