using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly JsonSerializerOptions ReplanStoredJson =
        new(JsonSerializerDefaults.Web);

    private static async Task<Guid> PublishReplacementReleaseAsync(
        string connectionString,
        DateTimeOffset now)
    {
        await SeedReplacementSourceAsync(connectionString, now);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await ApplicationDatabaseSession.SetAsync(
            db, new UserId(SupplierUserId), new TenantId(SupplierTenantId),
            CancellationToken.None);
        var tenantId = new TenantId(SupplierTenantId);
        await InventoryPublicationPersistence.LockSupplierAsync(
            db, tenantId, InventorySupplierId, CancellationToken.None);
        var release = await InventorySupplierReleasePublication.BeginAsync(
            db, tenantId, InventorySupplierId, ReplacementImportId,
            MasterDataCodes.InventoryReplacementModes.FullReplacement,
            SupplierUserId, now, CancellationToken.None);
        await InventoryPublicationPersistence.PersistAsync(
            db, tenantId, InventorySupplierId, ReplacementImportId,
            release.ReleaseId, SupplierUserId, now,
            [ReplacementPublication()], CancellationToken.None);
        var impacts = await InventorySupplierReleasePublication.CompleteAsync(
            db, tenantId, InventorySupplierId, ReplacementImportId,
            SupplierUserId, now, release, CancellationToken.None);
        Assert.Equal(1, impacts);
        await transaction.CommitAsync();
        return release.ReleaseId;
    }

    private static PreparedInventoryPublication ReplacementPublication()
    {
        var commercialTerms =
            "{\"vatTreatment\":\"INCLUSIVE\",\"minimumOrder\":1," +
            "\"conditions\":[\"Subject to supplier confirmation\"]," +
            "\"bookingLeadTimeDays\":5}";
        var rate = new PreparedInventoryRate(
            ReplacementRateId, "CPM", "ZAR", 1_100_000,
            new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31),
            "INCLUSIVE", commercialTerms, "replacement-rate-card.csv#row=2", null);
        var rates = JsonSerializer.Serialize(new[] { rate }, ReplanStoredJson);
        return new PreparedInventoryPublication(
            ProductId, "JHB-N1-001", false,
            ReplacementProductVersionId, 2, ReplacementCandidateId,
            "N1 Highway Digital Billboard", "OOH", "OOH_SITE", "Johannesburg",
            "Private supplier address", -26.100000m, 28.100000m, null,
            null, null, null, null, "{}", null,
            "{\"format\":\"Digital billboard\",\"buyingUnit\":\"screen/month\"," +
            "\"dimensions\":\"4m x 8m\",\"placement\":\"Highway\"," +
            "\"loopLengthSeconds\":60,\"slotLengthSeconds\":10," +
            "\"playsPerLoop\":1,\"quantity\":1}",
            "{\"country\":\"South Africa\",\"province\":\"Gauteng\"," +
            "\"municipality\":\"Johannesburg\",\"locality\":\"Johannesburg\"," +
            "\"road\":\"N1\"}",
            null, null, null, null,
            ReplacementRateId, rates, "CPM", "ZAR", 1_100_000,
            new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31),
            "INCLUSIVE", commercialTerms,
            ReplacementAvailabilityId, "AVAILABLE",
            null, null, null, null, null,
            "replacement-rate-card.csv#row=2",
            null, null, null, null, null, null);
    }

    private static async Task SeedReplacementSourceAsync(
        string connectionString,
        DateTimeOffset now)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        Add(batch, """
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code, status_code,
                scan_status_code, quarantine_object_key, protected_object_key,
                source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'replacement-rate-card.csv', 'text/csv',
                'documentClasses', 'CSV', 'COMPLETED', 'CLEAN',
                'replacement/quarantine', 'replacement/protected', repeat('b', 64),
                100, $4, 2, $5, $5)
            """, ReplacementImportId, SupplierTenantId, InventorySupplierId,
            SupplierUserId, now);
        var projectionId = InventoryProjectionSeed.Add(
            batch, SupplierTenantId, ReplacementImportId, SupplierUserId, now, 'b');
        Add(batch, """
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code,
                proposed_values_json, canonical_values_json, validation_json,
                source_locator, reviewed_by, version, created_at_utc,
                updated_at_utc, projection_id)
            VALUES ($1, $2, $3, 1, 'APPROVED', '{}', '{}', '[]',
                'replacement-rate-card.csv#row=2', $4, 1, $5, $5, $6)
            """, ReplacementCandidateId, SupplierTenantId, ReplacementImportId,
            SupplierUserId, now, projectionId);
        await batch.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> CreateSharedProposalForReplanAsync(
        HttpClient buyer,
        Guid planId,
        AdjustableMarketplaceClock clock)
    {
        using var generated = await CommandAsync(
            buyer, BuyerTenantId, $"briefs/{BuyerBriefId}/proposals:generate",
            "replan-proposal-generate", null, new
            {
                title = "Historical supplier release proposal",
                options = new[]
                {
                    new
                    {
                        planVersionId = planId,
                        label = "Original current placement",
                        outcome = "Retain the exact approved supplier placement.",
                    },
                },
                terms = "Historical proposal must remain immutable after supply changes.",
                expiryAtUtc = clock.GetUtcNow().AddDays(30),
            });
        var proposalId = generated.RootElement.GetProperty("id").GetGuid();
        using var unbranded = await CommandAsync(
            buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:approve-unbranded",
            "replan-proposal-unbranded", 1,
            new { reason = "Neutral layout approved for this acceptance journey." });
        using var approved = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:approve",
            "replan-proposal-approve", 2,
            new { reason = "Original plan and terms reviewed." });
        using var rendered = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:render",
            "replan-proposal-render", 3, new { });
        using var shared = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:share",
            "replan-proposal-share", 4,
            new { recipientUserId = ClientUserId, reason = "Send historical proposal." });
        Assert.Equal("SENT", shared.RootElement.GetProperty("status").GetString());
        return proposalId;
    }

    private static async Task<ProposalReplanAssessmentResult> AssessReplanAsync(
        string connectionString,
        ProposalReplanWorkerClaim claim)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        var processor = new ProposalReplanProcessor(
            new PlanningRecordStore(db), PlanningPolicy.Load());
        return await processor.AssessAsync(claim, CancellationToken.None);
    }

    private static async Task<ReplanHistoricalState> ReadHistoricalReplanStateAsync(
        string connectionString,
        Guid proposalId,
        Guid planId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT proposal.status_code, proposal.version,
                plan.status_code, plan.version,
                (SELECT count(*)::integer FROM commercial.shortlist_selections
                    WHERE tenant_id = @tenant),
                (SELECT count(*)::integer FROM commercial.media_plan_versions
                    WHERE tenant_id = @tenant),
                (SELECT count(*)::integer FROM commercial.proposal_versions
                    WHERE tenant_id = @tenant)
            FROM commercial.proposal_versions proposal
            CROSS JOIN commercial.media_plan_versions plan
            WHERE proposal.tenant_id = @tenant AND proposal.id = @proposal
              AND plan.tenant_id = @tenant AND plan.id = @plan
            """, connection);
        command.Parameters.AddWithValue("tenant", BuyerTenantId);
        command.Parameters.AddWithValue("proposal", proposalId);
        command.Parameters.AddWithValue("plan", planId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ReplanHistoricalState(
            reader.GetString(0), reader.GetInt64(1),
            reader.GetString(2), reader.GetInt64(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    private static async Task AssertHistoricalReplanStateUnchangedAsync(
        string connectionString,
        Guid proposalId,
        Guid planId,
        ReplanHistoricalState before)
    {
        var after = await ReadHistoricalReplanStateAsync(
            connectionString, proposalId, planId);
        Assert.Equal(before, after);
    }

    private static async Task AssertReleaseRegisteredOneReplanAsync(
        string connectionString,
        Guid proposalId,
        Guid releaseId)
    {
        var row = await ReadReplanRowAsync(connectionString, proposalId, releaseId);
        Assert.Equal("PENDING", row.Status);
        Assert.Equal(1, row.SourceGeneration);
        Assert.Equal(1, row.Count);
    }

    private static async Task<ReplanRow> AssertFinalReplanStateAsync(
        string connectionString,
        Guid proposalId,
        Guid releaseId,
        int expectedGeneration)
    {
        var row = await ReadReplanRowAsync(connectionString, proposalId, releaseId);
        Assert.Equal("REVIEW_REQUIRED", row.Status);
        Assert.Equal(expectedGeneration, row.SourceGeneration);
        Assert.Equal(1, row.Count);
        Assert.NotNull(row.ProposedRevisionJson);
        Assert.NotNull(row.ComparisonJson);
        using var proposed = JsonDocument.Parse(row.ProposedRevisionJson!);
        var line = Assert.Single(proposed.RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal(ReplacementProductVersionId,
            line.GetProperty("proposedProductVersionId").GetGuid());
        Assert.Equal(ReplacementRateId, line.GetProperty("proposedRateId").GetGuid());
        Assert.Equal(-150_000,
            line.GetProperty("supplierCostDeltaMinor").GetInt64());
        Assert.True(line.GetProperty("continuityCandidate").GetBoolean());
        return row;
    }

    private static async Task<ReplanRow> ReadReplanRowAsync(
        string connectionString,
        Guid proposalId,
        Guid releaseId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) OVER ()::integer, id, status_code, source_generation,
                proposed_revision_json::text, comparison_json::text
            FROM commercial.proposal_replan_revisions
            WHERE tenant_id = @tenant
              AND source_proposal_version_id = @proposal
              AND replacement_release_id = @release
            """, connection);
        command.Parameters.AddWithValue("tenant", BuyerTenantId);
        command.Parameters.AddWithValue("proposal", proposalId);
        command.Parameters.AddWithValue("release", releaseId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ReplanRow(
            reader.GetInt32(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private sealed record ReplanHistoricalState(
        string ProposalStatus,
        long ProposalVersion,
        string PlanStatus,
        long PlanVersion,
        int SelectionCount,
        int PlanCount,
        int ProposalCount);

    private sealed record ReplanRow(
        int Count,
        Guid Id,
        string Status,
        int SourceGeneration,
        string? ProposedRevisionJson,
        string? ComparisonJson);
}
