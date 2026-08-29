using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "commercial", table =>
        {
            table.HasCheckConstraint("ck_tenants_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_tenants");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new TenantId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.Type)
            .HasColumnName("type_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new TenantTypeCode(value));
        builder.Property(item => item.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(200);
        builder.Property(item => item.TradingName)
            .HasColumnName("trading_name")
            .HasMaxLength(200);
        builder.Property(item => item.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new Slug(value));
        builder.Property(item => item.Status)
            .HasColumnName("status_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
        builder.Property(item => item.TimeZone)
            .HasColumnName("timezone")
            .HasMaxLength(100);
        builder.Property(item => item.Currency)
            .HasColumnName("currency_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new CurrencyCode(value));
        builder.Property(item => item.VatStatus)
            .HasColumnName("vat_status_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new VatStatusCode(value));
        builder.Property(item => item.VatNumber)
            .HasColumnName("vat_number")
            .HasMaxLength(50);
        builder.Property(item => item.SettingsJson)
            .HasColumnName("settings_json")
            .HasColumnType("jsonb");
        builder.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        builder.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(item => item.Slug)
            .IsUnique()
            .HasDatabaseName("ux_tenants_slug");
        builder.HasIndex(item => new { item.Status, item.TradingName, item.Id })
            .HasDatabaseName("ix_tenants_status_name");
    }
}
