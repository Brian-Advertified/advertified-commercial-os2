using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("agencies", "commercial", table =>
        {
            table.HasCheckConstraint("ck_agencies_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_agencies");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new AgencyId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(value => value.Value, value => new TenantId(value));
        builder.Property(item => item.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(100);
        builder.Property(item => item.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(200);
        builder.Property(item => item.TradingName)
            .HasColumnName("trading_name")
            .HasMaxLength(200);
        builder.Property(item => item.Website)
            .HasColumnName("website")
            .HasMaxLength(2048);
        builder.Property(item => item.Status)
            .HasColumnName("status_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
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
            .HasDatabaseName("ux_agencies_tenant_id");
        builder.HasIndex(item => new { item.TenantId, item.ExternalReference })
            .IsUnique()
            .HasDatabaseName("ux_agencies_tenant_external_ref");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.TradingName, item.Id })
            .HasDatabaseName("ix_agencies_tenant_status_name");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_agencies_tenant");
    }
}
