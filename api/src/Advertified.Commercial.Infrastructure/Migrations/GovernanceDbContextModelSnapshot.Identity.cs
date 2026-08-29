using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class GovernanceDbContextModelSnapshot
{
    private static void BuildCommercialIdentityModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("tenants", "commercial", table =>
                table.HasCheckConstraint("ck_tenants_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_tenants");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new TenantId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.Type).HasColumnName("type_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new TenantTypeCode(value));
            builder.Property(item => item.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.TradingName).HasColumnName("trading_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.Slug).HasColumnName("slug").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new Slug(value));
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.TimeZone).HasColumnName("timezone").HasMaxLength(100).IsRequired();
            builder.Property(item => item.Currency).HasColumnName("currency_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new CurrencyCode(value));
            builder.Property(item => item.VatStatus).HasColumnName("vat_status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new VatStatusCode(value));
            builder.Property(item => item.VatNumber).HasColumnName("vat_number").HasMaxLength(50);
            builder.Property(item => item.SettingsJson).HasColumnName("settings_json").HasColumnType("jsonb").IsRequired();
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => item.Slug).IsUnique().HasDatabaseName("ux_tenants_slug");
            builder.HasIndex(item => new { item.Status, item.TradingName, item.Id })
                .HasDatabaseName("ix_tenants_status_name");
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users", "commercial", table =>
                table.HasCheckConstraint("ck_users_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_users");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new UserId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(320)
                .HasConversion(value => value.Value, value => new EmailAddress(value));
            builder.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(50);
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.MfaEnabled).HasColumnName("mfa_enabled");
            builder.Property(item => item.LastLoginAtUtc).HasColumnName("last_login_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => item.Email).IsUnique().HasDatabaseName("ux_users_email");
            builder.HasIndex(item => new { item.Status, item.DisplayName, item.Id })
                .HasDatabaseName("ix_users_status_name");
        });

        modelBuilder.Entity<Membership>(builder =>
        {
            builder.ToTable("memberships", "commercial", table =>
                table.HasCheckConstraint("ck_memberships_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_memberships");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new MembershipId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.UserId).HasColumnName("user_id")
                .HasConversion(value => value.Value, value => new UserId(value));
            builder.Property(item => item.Role).HasColumnName("role_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new RoleCode(value));
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.InvitedBy).HasColumnName("invited_by")
                .HasConversion(
                    value => value.HasValue ? value.Value.Value : (Guid?)null,
                    value => value.HasValue ? new UserId(value.Value) : null);
            builder.Property(item => item.InvitedAtUtc).HasColumnName("invited_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.AcceptedAtUtc).HasColumnName("accepted_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique()
                .HasDatabaseName("ux_memberships_tenant_id");
            builder.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique()
                .HasDatabaseName("ux_memberships_tenant_user");
            builder.HasIndex(item => new { item.UserId, item.Status, item.TenantId })
                .HasDatabaseName("ix_memberships_user_status");
            builder.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAtUtc, item.Id })
                .HasDatabaseName("ix_memberships_tenant_status_time");
            builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_memberships_tenant");
            builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_memberships_user");
        });
    }
}
