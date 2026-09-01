using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class GovernanceDbContextModelSnapshot
{
    private static void BuildCommercialPlatformModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecordRow>(builder =>
        {
            builder.ToTable("idempotency_records", "commercial");
            builder.HasKey(item => new { item.TenantId, item.Key })
                .HasName("pk_idempotency_records");
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.Key).HasColumnName("idempotency_key").HasMaxLength(200)
                .HasConversion(value => value.Value, value => new IdempotencyKey(value));
            builder.Property(item => item.CommandId).HasColumnName("command_id")
                .HasConversion(value => value.Value, value => new CommandId(value));
            builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64)
                .HasConversion(value => value.Value, value => new Sha256Digest(value));
            builder.Property(item => item.OutcomeJson).HasColumnName("outcome_json")
                .HasColumnType("jsonb").IsRequired();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new { item.TenantId, item.ExpiresAtUtc })
                .HasDatabaseName("ix_idempotency_tenant_expiry");
            builder.HasOne<Domain.Commercial.Tenant>().WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_idempotency_tenant");
        });

        modelBuilder.Entity<AuditEventRow>(builder =>
        {
            builder.ToTable("audit_events", "commercial", table =>
                table.HasCheckConstraint(
                    "ck_audit_events_resource_version",
                    "resource_version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_audit_events");
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.ActorId).HasColumnName("actor_id")
                .HasConversion(value => value.Value, value => new ActorId(value));
            builder.Property(item => item.CommandId).HasColumnName("command_id")
                .HasConversion(value => value.Value, value => new CommandId(value));
            builder.Property(item => item.CorrelationId).HasColumnName("correlation_id")
                .HasConversion(value => value.Value, value => new CorrelationId(value));
            builder.Property(item => item.Action).HasColumnName("action_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new ActionCode(value));
            builder.Property(item => item.ResourceType).HasColumnName("resource_type_code")
                .HasMaxLength(100)
                .HasConversion(value => value.Value, value => new ResourceTypeCode(value));
            builder.Property(item => item.ResourceId).HasColumnName("resource_id");
            builder.Property(item => item.ResourceVersion).HasColumnName("resource_version");
            builder.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.MetadataJson).HasColumnName("metadata_json")
                .HasColumnType("jsonb").IsRequired();
            builder.HasIndex(item => new { item.TenantId, item.OccurredAtUtc, item.Id })
                .HasDatabaseName("ix_audit_events_tenant_time");
            builder.HasIndex(item => new { item.TenantId, item.ResourceType, item.ResourceId })
                .HasDatabaseName("ix_audit_events_tenant_resource");
            builder.HasOne<Domain.Commercial.Tenant>().WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_audit_events_tenant");
        });

        modelBuilder.Entity<OutboxMessageRow>(builder =>
        {
            builder.ToTable("outbox_messages", "commercial", table =>
            {
                table.HasCheckConstraint(
                    "ck_outbox_aggregate_version",
                    "aggregate_version > 0");
                table.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");
                table.HasCheckConstraint(
                    "ck_outbox_dispatch_claim_shape",
                    "(claim_token IS NULL AND lease_owner IS NULL " +
                    "AND lease_expires_at_utc IS NULL AND attempt_started_at_utc IS NULL) OR " +
                    "(claim_token IS NOT NULL AND lease_owner IS NOT NULL " +
                    "AND lease_expires_at_utc IS NOT NULL AND attempt_started_at_utc IS NOT NULL " +
                    "AND lease_expires_at_utc > attempt_started_at_utc " +
                    "AND next_attempt_at_utc IS NULL AND attempts > 0)");
                table.HasCheckConstraint(
                    "ck_outbox_dispatch_failure_shape",
                    "(last_failure_code IS NULL AND last_failure_at_utc IS NULL) OR " +
                    "(last_failure_code IS NOT NULL AND last_failure_at_utc IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_outbox_dispatch_terminal_shape",
                    "NOT (published_at_utc IS NOT NULL AND dead_lettered_at_utc IS NOT NULL) " +
                    "AND ((published_at_utc IS NULL AND dead_lettered_at_utc IS NULL) OR " +
                    "(claim_token IS NULL AND next_attempt_at_utc IS NULL)) " +
                    "AND (dead_lettered_at_utc IS NULL OR (attempts > 0 " +
                    "AND last_failure_code IS NOT NULL AND last_failure_at_utc IS NOT NULL))");
                table.HasCheckConstraint(
                    "ck_outbox_dispatch_transport_reference",
                    "transport_reference IS NULL OR " +
                    "(published_at_utc IS NOT NULL AND btrim(transport_reference) <> '')");
                table.HasCheckConstraint(
                    "ck_outbox_dispatch_failure_code",
                    "last_failure_code IS NULL OR last_failure_code " +
                    "~ '^[A-Za-z0-9][A-Za-z0-9_.:-]{0,99}$'");
            });
            builder.HasKey(item => item.Id).HasName("pk_outbox_messages");
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.CausationId).HasColumnName("causation_id")
                .HasConversion(value => value.Value, value => new CommandId(value));
            builder.Property(item => item.CorrelationId).HasColumnName("correlation_id")
                .HasConversion(value => value.Value, value => new CorrelationId(value));
            builder.Property(item => item.EventType).HasColumnName("event_type_code")
                .HasMaxLength(100)
                .HasConversion(value => value.Value, value => new EventTypeCode(value));
            builder.Property(item => item.AggregateType).HasColumnName("aggregate_type_code")
                .HasMaxLength(100)
                .HasConversion(value => value.Value, value => new ResourceTypeCode(value));
            builder.Property(item => item.AggregateId).HasColumnName("aggregate_id");
            builder.Property(item => item.AggregateVersion).HasColumnName("aggregate_version");
            builder.Property(item => item.PayloadJson).HasColumnName("payload_json")
                .HasColumnType("jsonb").IsRequired();
            builder.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.PublishedAtUtc).HasColumnName("published_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.Attempts).HasColumnName("attempts");
            builder.Property(item => item.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.ClaimToken).HasColumnName("claim_token");
            builder.Property(item => item.LeaseOwner).HasColumnName("lease_owner");
            builder.Property(item => item.LeaseExpiresAtUtc)
                .HasColumnName("lease_expires_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.AttemptStartedAtUtc)
                .HasColumnName("attempt_started_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.TransportReference)
                .HasColumnName("transport_reference").HasMaxLength(300);
            builder.Property(item => item.LastFailureCode)
                .HasColumnName("last_failure_code").HasMaxLength(100);
            builder.Property(item => item.LastFailureAtUtc)
                .HasColumnName("last_failure_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.DeadLetteredAtUtc)
                .HasColumnName("dead_lettered_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new
            {
                item.NextAttemptAtUtc,
                item.LeaseExpiresAtUtc,
                item.OccurredAtUtc,
                item.Id,
            })
                .HasDatabaseName("ix_outbox_dispatch_due")
                .HasFilter("published_at_utc IS NULL AND dead_lettered_at_utc IS NULL");
            builder.HasIndex(item => item.ClaimToken).IsUnique()
                .HasDatabaseName("ux_outbox_dispatch_claim_token")
                .HasFilter("claim_token IS NOT NULL");
            builder.HasIndex(item => item.TenantId)
                .HasDatabaseName("IX_outbox_messages_tenant_id");
            builder.HasOne<Domain.Commercial.Tenant>().WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_outbox_tenant");
        });
    }
}
