using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalCommercialFoundation
{
    private static void CreatePlatformTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.idempotency_records (
                tenant_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                command_id uuid NOT NULL,
                request_hash varchar(64) NOT NULL,
                outcome_json jsonb NOT NULL,
                created_at_utc timestamptz NOT NULL,
                expires_at_utc timestamptz NOT NULL,
                CONSTRAINT pk_idempotency_records
                    PRIMARY KEY (tenant_id, idempotency_key),
                CONSTRAINT ck_idempotency_hash
                    CHECK (request_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_idempotency_expiry
                    CHECK (expires_at_utc > created_at_utc),
                CONSTRAINT fk_idempotency_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id)
            );

            CREATE INDEX ix_idempotency_tenant_expiry
                ON commercial.idempotency_records (tenant_id, expires_at_utc);

            CREATE TABLE commercial.audit_events (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                actor_id uuid NOT NULL,
                command_id uuid NOT NULL,
                correlation_id uuid NOT NULL,
                action_code varchar(100) NOT NULL,
                resource_type_code varchar(100) NOT NULL,
                resource_id uuid NOT NULL,
                resource_version bigint NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                metadata_json jsonb NOT NULL,
                CONSTRAINT ck_audit_events_resource_version
                    CHECK (resource_version > 0),
                CONSTRAINT fk_audit_events_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id)
            );

            CREATE INDEX ix_audit_events_tenant_time
                ON commercial.audit_events (tenant_id, occurred_at_utc, id);
            CREATE INDEX ix_audit_events_tenant_resource
                ON commercial.audit_events
                (tenant_id, resource_type_code, resource_id);

            CREATE TABLE commercial.outbox_messages (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                causation_id uuid NOT NULL,
                correlation_id uuid NOT NULL,
                event_type_code varchar(100) NOT NULL,
                aggregate_type_code varchar(100) NOT NULL,
                aggregate_id uuid NOT NULL,
                aggregate_version bigint NOT NULL,
                payload_json jsonb NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                published_at_utc timestamptz,
                attempts integer NOT NULL DEFAULT 0,
                CONSTRAINT ck_outbox_aggregate_version
                    CHECK (aggregate_version > 0),
                CONSTRAINT ck_outbox_attempts CHECK (attempts >= 0),
                CONSTRAINT fk_outbox_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id)
            );

            CREATE INDEX ix_outbox_unpublished_time
                ON commercial.outbox_messages
                (published_at_utc, occurred_at_utc, id);
            CREATE INDEX "IX_outbox_messages_tenant_id"
                ON commercial.outbox_messages (tenant_id);
            """);
    }
}
