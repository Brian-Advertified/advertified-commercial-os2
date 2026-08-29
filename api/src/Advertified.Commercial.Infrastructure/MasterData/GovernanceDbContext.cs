using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.MasterData;

public sealed class GovernanceDbContext(DbContextOptions<GovernanceDbContext> options)
    : DbContext(options)
{
    public DbSet<MasterDataSet> MasterDataSets => Set<MasterDataSet>();

    public DbSet<MasterDataItem> MasterDataItems => Set<MasterDataItem>();

    public DbSet<MasterDataItemHistory> MasterDataItemHistory => Set<MasterDataItemHistory>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<ClientAccount> ClientAccounts => Set<ClientAccount>();

    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<IdempotencyRecordRow> IdempotencyRecords => Set<IdempotencyRecordRow>();

    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCollection(modelBuilder);
        ConfigureItem(modelBuilder);
        ConfigureHistory(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceDbContext).Assembly);
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MasterDataSet>();
        entity.ToTable("master_data_collections", "governance");
        entity.HasKey(item => item.Code).HasName("pk_master_data_collections");
        entity.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .ValueGeneratedNever();
        entity.Property(item => item.RegistryVersion)
            .HasColumnName("registry_version")
            .HasMaxLength(50);
        entity.Property(item => item.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("date");
        entity.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
    }

    private static void ConfigureItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MasterDataItem>();
        entity.ToTable(
            "master_data_items",
            "governance",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_master_data_items_sort_order",
                    "sort_order > 0");
                table.HasCheckConstraint(
                    "ck_master_data_items_effective_dates",
                    "effective_to IS NULL OR effective_to > effective_from");
            });
        entity.HasKey(item => new { item.CollectionCode, item.Code })
            .HasName("pk_master_data_items");
        entity.Property(item => item.CollectionCode)
            .HasColumnName("collection_code")
            .HasMaxLength(100);
        entity.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .ValueGeneratedNever();
        entity.Property(item => item.DisplayLabel)
            .HasColumnName("display_label")
            .HasMaxLength(200);
        entity.Property(item => item.IsActive).HasColumnName("is_active");
        entity.Property(item => item.SortOrder).HasColumnName("sort_order");
        entity.Property(item => item.MetadataJson)
            .HasColumnName("metadata_json")
            .HasColumnType("jsonb");
        entity.Property(item => item.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("date");
        entity.Property(item => item.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("date");
        entity.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(item => new { item.CollectionCode, item.SortOrder })
            .IsUnique()
            .HasDatabaseName("ux_master_data_items_collection_sort");
        entity.HasOne<MasterDataSet>()
            .WithMany()
            .HasForeignKey(item => item.CollectionCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_master_data_items_collections");
    }

    private static void ConfigureHistory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MasterDataItemHistory>();
        entity.ToTable("master_data_item_history", "governance");
        entity.HasKey(item => item.Id).HasName("pk_master_data_item_history");
        entity.Property(item => item.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        entity.Property(item => item.CollectionCode)
            .HasColumnName("collection_code")
            .HasMaxLength(100);
        entity.Property(item => item.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(100);
        entity.Property(item => item.Operation)
            .HasColumnName("operation")
            .HasMaxLength(10);
        entity.Property(item => item.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("jsonb");
        entity.Property(item => item.ChangedAtUtc)
            .HasColumnName("changed_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(item => new { item.CollectionCode, item.ItemCode, item.ChangedAtUtc })
            .HasDatabaseName("ix_master_data_history_item_time");
    }
}
