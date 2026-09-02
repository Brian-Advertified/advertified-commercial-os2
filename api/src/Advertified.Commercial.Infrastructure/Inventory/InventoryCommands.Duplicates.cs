using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private const string EmbeddingTextSchemaVersion = "inventory-embedding-text-v1";

    private async Task<CommandOutcome> SubmitEmbeddingOutcomeAsync(
        Guid productId,
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var product = await LoadEmbeddingProductAsync(
            productId, envelope, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        if (product.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        var canonicalText = InventoryEmbeddingTextBuilder.Build(product);
        var inputHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText))).ToLowerInvariant();
        var expected = ExpectedProvider();
        var existing = await FindEmbeddingAsync(
            envelope, product.ProductVersionId, expected.Provider,
            expected.Model, inputHash, cancellationToken);
        if (existing is not null && !envelope.Command.ForceBackfill)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (envelope.Command.ForceBackfill)
        {
            await EnsurePlatformAdministratorAsync(envelope, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        await AcquireEmbeddingBudgetLockAsync(envelope, cancellationToken);
        var monthCost = await GetMonthlyEmbeddingCostAsync(
            envelope, now, cancellationToken);
        EnsureEmbeddingBudget(monthCost, embeddingGenerator.MaximumCostUsdMicros);
        var generated = await embeddingGenerator.GenerateAsync(
            canonicalText, cancellationToken);
        ValidateGeneration(generated, expected);
        EnsureEmbeddingBudget(monthCost, generated.IncrementalCostUsdMicros);

        var jobId = Guid.NewGuid();
        var embeddingId = Guid.NewGuid();
        await InsertEmbeddingAsync(
            envelope, product, generated, inputHash, jobId, embeddingId,
            envelope.Command.ForceBackfill, now, cancellationToken);
        await DetectSemanticDuplicateCandidatesAsync(
            envelope, product, generated, embeddingId, now, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var cumulativeCost = monthCost + generated.IncrementalCostUsdMicros;
        var budget = embeddingOptions.MonthlyBudgetUsdMicros;
        var view = new InventoryEmbeddingView(
            embeddingId, productId, product.ProductVersionId,
            generated.Provider, generated.Model, inputHash,
            InventoryEmbeddingOptions.Dimensions, now, 1, jobId,
            generated.InputTokens, generated.IncrementalCostUsdMicros,
            cumulativeCost, budget, cumulativeCost >= budget * 8 / 10);
        return OpportunityCommandSupport.Outcome(
            envelope, view, productId, product.Version + 1,
            MasterDataReferences.CommercialResourceTypes.InventoryProduct,
            MasterDataReferences.CommercialActions.InventoryEmbeddingSubmitted,
            MasterDataReferences.CommercialEventTypes.InventoryEmbeddingSubmitted, now);
    }

    private Task<EmbeddingProductRow?> LoadEmbeddingProductAsync(
        Guid productId,
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<EmbeddingProductRow>($"""
            SELECT product.id AS "ProductId", product.current_version_id AS "ProductVersionId",
                product.version AS "Version",
                product.supplier_product_code AS "ProductCode",
                version.name AS "Name", version.channel_code AS "Channel",
                version.product_type_code AS "ProductType",
                version.geography AS "Geography", version.description AS "Description"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id
             AND version.id = product.current_version_id
            WHERE product.tenant_id = {envelope.TenantId.Value} AND product.id = {productId}
              AND product.current_version_id = {envelope.Command.ProductVersionId}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            FOR UPDATE OF product
            """).SingleOrDefaultAsync(cancellationToken);

    private async Task InsertEmbeddingAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        EmbeddingProductRow product,
        InventoryEmbeddingGeneration generation,
        string inputHash,
        Guid jobId,
        Guid embeddingId,
        bool isExplicitBackfill,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var literal = "[" + string.Join(',', generation.Embedding.Select(value =>
            value.ToString("R", CultureInfo.InvariantCulture))) + "]";
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_embedding_jobs (
                id, tenant_id, product_version_id, provider_code, model_code,
                dimensions, region_code, text_schema_version, normalized, input_hash,
                provider_request_id, input_tokens, incremental_cost_usd_micros,
                requested_by, generated_at_utc, is_explicit_backfill)
            VALUES ({jobId}, {envelope.TenantId.Value}, {product.ProductVersionId},
                {generation.Provider}, {generation.Model},
                {InventoryEmbeddingOptions.Dimensions}, {generation.Region},
                {EmbeddingTextSchemaVersion}, true, {inputHash},
                {generation.ProviderRequestId}, {generation.InputTokens},
                {generation.IncrementalCostUsdMicros}, {envelope.ActorId.Value}, {now},
                {isExplicitBackfill});
            INSERT INTO commercial.inventory_product_embeddings (
                id, tenant_id, product_version_id, provider_code, model_code,
                dimensions, input_hash, embedding, created_by, created_at_utc, job_id)
            VALUES ({embeddingId}, {envelope.TenantId.Value}, {product.ProductVersionId},
                {generation.Provider}, {generation.Model},
                {InventoryEmbeddingOptions.Dimensions}, {inputHash}, {literal}::vector,
                {envelope.ActorId.Value}, {now}, {jobId})
            """, cancellationToken);
    }

    private Task<EmbeddingRow?> FindEmbeddingAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        Guid productVersionId,
        string provider,
        string model,
        string inputHash,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<EmbeddingRow>($"""
            SELECT id AS "Id", created_at_utc AS "CreatedAtUtc"
            FROM commercial.inventory_product_embeddings
            WHERE tenant_id = {envelope.TenantId.Value}
              AND product_version_id = {productVersionId}
              AND provider_code = {provider} AND model_code = {model}
              AND input_hash = {inputHash}
            ORDER BY created_at_utc DESC, id DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<int> DetectSemanticDuplicateCandidatesAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        EmbeddingProductRow product,
        InventoryEmbeddingGeneration generation,
        Guid embeddingId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_duplicate_candidates (
                id, tenant_id, left_product_id, right_product_id,
                left_product_version_id, right_product_version_id,
                method_code, similarity, evidence_json, status_code,
                detected_at_utc, version)
            SELECT gen_random_uuid(), {envelope.TenantId.Value},
                CASE WHEN source.id < peer.id THEN source.id ELSE peer.id END,
                CASE WHEN source.id < peer.id THEN peer.id ELSE source.id END,
                CASE WHEN source.id < peer.id THEN source.current_version_id
                    ELSE peer.current_version_id END,
                CASE WHEN source.id < peer.id THEN peer.current_version_id
                    ELSE source.current_version_id END,
                {MasterDataCodes.InventoryDuplicateMethods.SemanticVector},
                (1 - (source_embedding.embedding <=> peer_embedding.embedding))::numeric,
                jsonb_build_object(
                    'provider', {generation.Provider}, 'model', {generation.Model},
                    'sourceEmbeddingId', {embeddingId},
                    'peerEmbeddingId', peer_embedding.id,
                    'threshold', {duplicatePolicy.SemanticCandidateThreshold}),
                {MasterDataCodes.InventoryDuplicateStatuses.Open}, {now}, 1
            FROM commercial.inventory_products source
            JOIN commercial.inventory_product_embeddings source_embedding
              ON source_embedding.tenant_id = source.tenant_id
             AND source_embedding.id = {embeddingId}
            JOIN commercial.inventory_products peer
              ON peer.tenant_id = source.tenant_id AND peer.id <> source.id
             AND peer.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            JOIN LATERAL (
                SELECT candidate.*
                FROM commercial.inventory_product_embeddings candidate
                WHERE candidate.tenant_id = peer.tenant_id
                  AND candidate.product_version_id = peer.current_version_id
                  AND candidate.provider_code = {generation.Provider}
                  AND candidate.model_code = {generation.Model}
                ORDER BY candidate.created_at_utc DESC, candidate.id DESC
                LIMIT 1) peer_embedding ON TRUE
            WHERE source.tenant_id = {envelope.TenantId.Value}
              AND source.id = {product.ProductId}
              AND 1 - (source_embedding.embedding <=> peer_embedding.embedding) >=
                  {duplicatePolicy.SemanticCandidateThreshold}
              AND NOT EXISTS (
                  SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                  WHERE identity_link.tenant_id = source.tenant_id
                    AND identity_link.duplicate_product_id IN (source.id, peer.id))
            ON CONFLICT (
                tenant_id, left_product_version_id, right_product_version_id, method_code)
            DO NOTHING
            """, cancellationToken);

    private Task<int> AcquireEmbeddingBudgetLockAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_advisory_xact_lock(
                hashtextextended({envelope.TenantId.Value}::text, 20260902))
            """, cancellationToken);

    private async Task EnsurePlatformAdministratorAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var allowed = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.memberships membership
                WHERE membership.tenant_id = {envelope.TenantId.Value}
                  AND membership.user_id = {envelope.ActorId.Value}
                  AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                  AND membership.role_code = {MasterDataCodes.Roles.PlatformAdmin}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!allowed)
        {
            throw new UnauthorizedAccessException(
                "Only a platform administrator may force an embedding backfill.");
        }
    }

    private Task<long> GetMonthlyEmbeddingCostAsync(
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTimeOffset(
            now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return store.DbContext.Database.SqlQuery<long>($"""
            SELECT COALESCE(SUM(incremental_cost_usd_micros), 0)::bigint AS "Value"
            FROM commercial.inventory_embedding_jobs
            WHERE tenant_id = {envelope.TenantId.Value}
              AND generated_at_utc >= {monthStart} AND generated_at_utc < {monthStart.AddMonths(1)}
            """).SingleAsync(cancellationToken);
    }

    private (string Provider, string Model) ExpectedProvider() =>
        embeddingOptions.Mode == InventoryEmbeddingOptions.BedrockHttpMode
            ? ("bedrock", InventoryEmbeddingOptions.TitanModel)
            : ("deterministic", "fixture-inventory-embedding-v1");

    private void EnsureEmbeddingBudget(long currentCost, long additionalCost)
    {
        if (additionalCost < 0 ||
            currentCost > embeddingOptions.MonthlyBudgetUsdMicros - additionalCost)
        {
            throw new InvalidLifecycleTransitionException();
        }
    }

    private static void ValidateGeneration(
        InventoryEmbeddingGeneration generation,
        (string Provider, string Model) expected)
    {
        var magnitude = Math.Sqrt(generation.Embedding.Sum(value => value * value));
        if (generation.Provider != expected.Provider || generation.Model != expected.Model ||
            generation.Embedding.Count != InventoryEmbeddingOptions.Dimensions ||
            generation.Embedding.Any(value => !float.IsFinite(value)) ||
            magnitude is < 0.999 or > 1.001 || generation.InputTokens < 0 ||
            string.IsNullOrWhiteSpace(generation.ProviderRequestId))
        {
            throw new InvalidOperationException("The generated embedding is invalid.");
        }
    }

    internal sealed record EmbeddingProductRow(
        Guid ProductId,
        Guid ProductVersionId,
        long Version,
        string ProductCode,
        string Name,
        string Channel,
        string ProductType,
        string Geography,
        string? Description);

    private sealed record EmbeddingRow(Guid Id, DateTimeOffset CreatedAtUtc);
}
