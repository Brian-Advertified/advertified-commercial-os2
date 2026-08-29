using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "commercial", table =>
        {
            table.HasCheckConstraint("ck_users_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_users");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new UserId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .HasConversion(value => value.Value, value => new EmailAddress(value));
        builder.Property(item => item.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200);
        builder.Property(item => item.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);
        builder.Property(item => item.Status)
            .HasColumnName("status_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
        builder.Property(item => item.MfaEnabled).HasColumnName("mfa_enabled");
        builder.Property(item => item.LastLoginAtUtc)
            .HasColumnName("last_login_at_utc")
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
        builder.HasIndex(item => item.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");
        builder.HasIndex(item => new { item.Status, item.DisplayName, item.Id })
            .HasDatabaseName("ix_users_status_name");
    }
}
