using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class GovernanceDbContextModelSnapshot
{
    private static void BuildCommercialBusinessModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientAccount>(builder =>
        {
            builder.ToTable("client_accounts", "commercial", table =>
                table.HasCheckConstraint("ck_client_accounts_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_client_accounts");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new ClientAccountId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.ExternalReference).HasColumnName("external_reference")
                .HasMaxLength(100).IsRequired();
            builder.Property(item => item.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.TradingName).HasColumnName("trading_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.Website).HasColumnName("website").HasMaxLength(2048);
            builder.Property(item => item.Industry).HasColumnName("industry").HasMaxLength(100);
            builder.Property(item => item.BillingProfileJson).HasColumnName("billing_profile_json")
                .HasColumnType("jsonb").IsRequired();
            builder.Property(item => item.PrimaryContactId).HasColumnName("primary_contact_id")
                .HasConversion(
                    value => value.HasValue ? value.Value.Value : (Guid?)null,
                    value => value.HasValue ? new ContactId(value.Value) : null);
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique()
                .HasDatabaseName("ux_client_accounts_tenant_id");
            builder.HasIndex(item => new { item.TenantId, item.ExternalReference }).IsUnique()
                .HasDatabaseName("ux_client_accounts_tenant_external_ref");
            builder.HasIndex(item => new { item.TenantId, item.Status, item.TradingName, item.Id })
                .HasDatabaseName("ix_client_accounts_tenant_status_name");
            builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_client_accounts_tenant");
        });

        modelBuilder.Entity<Agency>(builder =>
        {
            builder.ToTable("agencies", "commercial", table =>
                table.HasCheckConstraint("ck_agencies_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_agencies");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new AgencyId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.ExternalReference).HasColumnName("external_reference")
                .HasMaxLength(100).IsRequired();
            builder.Property(item => item.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.TradingName).HasColumnName("trading_name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.Website).HasColumnName("website").HasMaxLength(2048);
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique()
                .HasDatabaseName("ux_agencies_tenant_id");
            builder.HasIndex(item => new { item.TenantId, item.ExternalReference }).IsUnique()
                .HasDatabaseName("ux_agencies_tenant_external_ref");
            builder.HasIndex(item => new { item.TenantId, item.Status, item.TradingName, item.Id })
                .HasDatabaseName("ix_agencies_tenant_status_name");
            builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_agencies_tenant");
        });

        modelBuilder.Entity<Contact>(builder =>
        {
            builder.ToTable("contacts", "commercial", table =>
                table.HasCheckConstraint("ck_contacts_version", "version > 0"));
            builder.HasKey(item => item.Id).HasName("pk_contacts");
            builder.Property(item => item.Id).HasColumnName("id")
                .HasConversion(value => value.Value, value => new ContactId(value))
                .ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id")
                .HasConversion(value => value.Value, value => new TenantId(value));
            builder.Property(item => item.ClientAccountId).HasColumnName("client_account_id")
                .HasConversion(value => value.Value, value => new ClientAccountId(value));
            builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(100);
            builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(320)
                .HasConversion(value => value.Value, value => new EmailAddress(value));
            builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(50);
            builder.Property(item => item.Purpose).HasColumnName("purpose_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new ContactPurposeCode(value));
            builder.Property(item => item.ConsentBasis).HasColumnName("consent_basis")
                .HasMaxLength(500).IsRequired();
            builder.Property(item => item.RetainUntil).HasColumnName("retain_until")
                .HasColumnType("date");
            builder.Property(item => item.Status).HasColumnName("status_code").HasMaxLength(100)
                .HasConversion(value => value.Value, value => new LifecycleStatusCode(value));
            builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc")
                .HasColumnType("timestamp with time zone");
            builder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique()
                .HasDatabaseName("ux_contacts_tenant_id");
            builder.HasIndex(item => new { item.TenantId, item.Status, item.Name, item.Id })
                .HasDatabaseName("ix_contacts_tenant_status_name");
            builder.HasIndex(item => new { item.TenantId, item.ClientAccountId })
                .HasDatabaseName("IX_contacts_tenant_id_client_account_id");
            builder.HasOne<ClientAccount>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ClientAccountId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_contacts_tenant_client_account");
        });
    }
}
