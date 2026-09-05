using System.Diagnostics;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryExtractionDurabilityTests
{
    private static async Task SeedPagingCandidatesAsync(string connectionString)
    {
        var artifactId = Guid.Parse("de500000-0000-0000-0000-000000000010");
        var projectionId = Guid.Parse("de700000-0000-0000-0000-000000000001");
        var scheduler = new WorkerSchedulerStore(connectionString);
        var claim = Assert.IsType<InventoryExtractionWorkerClaim>(
            await scheduler.ClaimInventoryExtractionAsync(
                Guid.NewGuid(), 30, 1, CancellationToken.None));
        await InsertArtifactAsync(connectionString, artifactId, claim.AttemptId);
        await ExecuteAsync(connectionString, """
            INSERT INTO commercial.inventory_extraction_projections (
                id, tenant_id, import_id, input_artifact_id, attempt_id,
                projector_code, projector_version, schema_version, canonical_json,
                canonical_output_hash, candidate_count, created_by, created_at_utc)
            VALUES (@projection, @tenant, @import, @artifact, @attempt,
                'generic-inventory', '4.0.0', 'inventory-extraction/4', '{}'::jsonb,
                repeat('d', 64), 5001, @user, @now);
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code,
                proposed_values_json, canonical_values_json, validation_json,
                source_locator, version, created_at_utc, updated_at_utc, projection_id)
            SELECT gen_random_uuid(), @tenant, @import, item,
                'REVIEW_REQUIRED', jsonb_build_object('name', 'Product ' || item),
                jsonb_build_object('name', 'Product ' || item), '[]'::jsonb,
                'fixture:row=' || item, 1, @now, @now, @projection
            FROM generate_series(1, 5001) AS item;
            """, ("projection", projectionId), ("tenant", TenantId),
            ("import", ImportId), ("artifact", artifactId),
            ("attempt", claim.AttemptId), ("user", UserId), ("now", Now));
    }

    private static async Task AssertCandidatePaginationAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        var store = new InventoryRecordStore(db, null!, null!);
        InventoryCandidateCursorValue? cursor = null;
        var seen = new HashSet<Guid>();
        var timer = Stopwatch.StartNew();
        do
        {
            var page = await store.ListCandidatePageAsync(new TenantId(TenantId),
                ImportId, cursor, 101, CancellationToken.None);
            var selected = page.Take(100).ToArray();
            Assert.All(selected, candidate => Assert.True(seen.Add(candidate.Id)));
            cursor = page.Count > 100
                ? new(selected[^1].RowNumber, selected[^1].Id)
                : null;
        } while (cursor is not null);
        timer.Stop();
        Assert.Equal(5_001, seen.Count);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }
}
