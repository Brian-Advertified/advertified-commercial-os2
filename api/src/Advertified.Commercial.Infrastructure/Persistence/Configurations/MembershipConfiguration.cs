using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships", "commercial", table =>
        {
            table.HasCheckConstraint("ck_memberships_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_memberships");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new MembershipId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(value => value.Value, value => new TenantId(value));
        builder.Property(item => item.UserId)
            .HasColumnName("user_id")
            .HasConversion(value => value.Value, value => new UserId(value));
        builder.Property(item => item.Role)
            .HasColumnName("role_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new RoleCode(value));
        builder.Property(item => item.Status)
            .HasColumnName("status_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
        builder.Property(item => item.InvitedBy)
            .HasColumnName("invited_by")
            .HasConversion(
                value => value.HasValue ? value.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);
        builder.Property(item => item.InvitedAtUtc)
            .HasColumnName("invited_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(item => item.AcceptedAtUtc)
            .HasColumnName("accepted_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.TenantId, item.Id })
            .IsUnique()
            .HasDatabaseName("ux_memberships_tenant_id");
        builder.HasIndex(item => new { item.TenantId, item.UserId })
            .IsUnique()
            .HasDatabaseName("ux_memberships_tenant_user");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAtUtc, item.Id })
            .HasDatabaseName("ix_memberships_tenant_status_time");
        builder.HasIndex(item => new { item.UserId, item.Status, item.TenantId })
            .HasDatabaseName("ix_memberships_user_status");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_memberships_tenant");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_memberships_user");
    }
}
