using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advertified.Commercial.Infrastructure.Persistence.Configurations;

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts", "commercial", table =>
        {
            table.HasCheckConstraint("ck_contacts_version", "version > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_contacts");
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(value => value.Value, value => new ContactId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(value => value.Value, value => new TenantId(value));
        builder.Property(item => item.ClientAccountId)
            .HasColumnName("client_account_id")
            .HasConversion(value => value.Value, value => new ClientAccountId(value));
        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(200);
        builder.Property(item => item.JobTitle)
            .HasColumnName("job_title")
            .HasMaxLength(100);
        builder.Property(item => item.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .HasConversion(value => value.Value, value => new EmailAddress(value));
        builder.Property(item => item.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);
        builder.Property(item => item.Purpose)
            .HasColumnName("purpose_code")
            .HasMaxLength(100)
            .HasConversion(value => value.Value, value => new ContactPurposeCode(value));
        builder.Property(item => item.ConsentBasis)
            .HasColumnName("consent_basis")
            .HasMaxLength(500);
        builder.Property(item => item.RetainUntil)
            .HasColumnName("retain_until")
            .HasColumnType("date");
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
            .HasDatabaseName("ux_contacts_tenant_id");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.Name, item.Id })
            .HasDatabaseName("ix_contacts_tenant_status_name");
        builder.HasOne<ClientAccount>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ClientAccountId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contacts_tenant_client_account");
    }
}
