using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class ClientAccountConfiguration : IEntityTypeConfiguration<ClientAccount>
{
    public void Configure(EntityTypeBuilder<ClientAccount> builder)
    {
        builder.ToTable("client_accounts", "commercial", table =>
        {
            table.HasCheckConstraint("ck_client_accounts_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_client_accounts");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new ClientAccountId(value))
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
        builder.Property(item => item.Industry)
            .HasColumnName("industry")
            .HasMaxLength(100);
        builder.Property(item => item.BillingProfileJson)
            .HasColumnName("billing_profile_json")
            .HasColumnType("jsonb");
        builder.Property(item => item.PrimaryContactId)
            .HasColumnName("primary_contact_id")
            .HasConversion(
                value => value.HasValue ? value.Value.Value : (Guid?)null,
                value => value.HasValue ? new ContactId(value.Value) : null);
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
            .HasDatabaseName("ux_client_accounts_tenant_id");
        builder.HasIndex(item => new { item.TenantId, item.ExternalReference })
            .IsUnique()
            .HasDatabaseName("ux_client_accounts_tenant_external_ref");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.TradingName, item.Id })
            .HasDatabaseName("ix_client_accounts_tenant_status_name");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_client_accounts_tenant");
    }
}
