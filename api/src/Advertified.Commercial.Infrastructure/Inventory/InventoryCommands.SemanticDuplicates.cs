using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private async Task<CommandOutcome> NominateSemanticDuplicateOutcomeAsync(
        Guid productId,
        CommandEnvelope<NominateInventorySemanticDuplicateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        if (productId == command.PeerProductId ||
            command.ProductVersionId == command.PeerProductVersionId)
        {
            throw new ArgumentException("A distinct semantic recall peer is required.");
        }
        var reason = OpportunityCommandSupport.Required(
            command.Reason, 2_000, nameof(command.Reason));
        var pair = await LoadSemanticPairAsync(productId, envelope, cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        var sourceFirst = pair.SourceFirst;
        var leftProductId = sourceFirst ? productId : command.PeerProductId;
        var rightProductId = sourceFirst ? command.PeerProductId : productId;
        var leftVersionId = sourceFirst
            ? command.ProductVersionId : command.PeerProductVersionId;
        var rightVersionId = sourceFirst
            ? command.PeerProductVersionId : command.ProductVersionId;
        var leftEmbeddingId = sourceFirst ? pair.SourceEmbeddingId : pair.PeerEmbeddingId;
        var rightEmbeddingId = sourceFirst ? pair.PeerEmbeddingId : pair.SourceEmbeddingId;
        var evidenceJson = JsonSerializer.Serialize(new
        {
            pair.Provider,
            pair.Model,
            leftEmbeddingId,
            rightEmbeddingId,
            nominatedBy = envelope.ActorId.Value,
            reason,
        });
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var created = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_duplicate_candidates (
                id, tenant_id, left_product_id, right_product_id,
                left_product_version_id, right_product_version_id,
                method_code, similarity, evidence_json, status_code,
                detected_at_utc, version)
            VALUES ({id}, {envelope.TenantId.Value}, {leftProductId}, {rightProductId},
                {leftVersionId}, {rightVersionId},
                {MasterDataCodes.InventoryDuplicateMethods.SemanticVector},
                {pair.Similarity}, {evidenceJson}::jsonb,
                {MasterDataCodes.InventoryDuplicateStatuses.Open}, {now}, 1)
            ON CONFLICT (
                tenant_id, left_product_version_id, right_product_version_id, method_code)
            DO NOTHING
            """, cancellationToken);
        if (created != 1) throw new InvalidLifecycleTransitionException();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var candidate = await FindSemanticCandidateAsync(
            envelope, leftVersionId, rightVersionId, cancellationToken)
            ?? throw new InvalidOperationException(
                "The semantic duplicate candidate was not persisted.");
        return OpportunityCommandSupport.Outcome(
            envelope, candidate.ToView(), productId, envelope.ExpectedVersion + 1,
            MasterDataReferences.CommercialResourceTypes.InventoryProduct,
            MasterDataReferences.CommercialActions.InventoryDuplicateNominated,
            MasterDataReferences.CommercialEventTypes.InventoryDuplicateNominated, now);
    }

    private Task<SemanticDuplicatePairRow?> LoadSemanticPairAsync(
        Guid productId,
        CommandEnvelope<NominateInventorySemanticDuplicateCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<SemanticDuplicatePairRow>($"""
            SELECT source_embedding.id AS "SourceEmbeddingId",
                peer_embedding.id AS "PeerEmbeddingId",
                (source.id < peer.id) AS "SourceFirst",
                source_embedding.provider_code AS "Provider",
                source_embedding.model_code AS "Model",
                (1 - (source_embedding.embedding <=> peer_embedding.embedding))::numeric
                    AS "Similarity"
            FROM commercial.inventory_products source
            JOIN commercial.inventory_product_embeddings source_embedding
              ON source_embedding.tenant_id = source.tenant_id
             AND source_embedding.product_version_id = source.current_version_id
            JOIN commercial.inventory_products peer
              ON peer.tenant_id = source.tenant_id
             AND peer.id = {envelope.Command.PeerProductId}
            JOIN commercial.inventory_product_embeddings peer_embedding
              ON peer_embedding.tenant_id = peer.tenant_id
             AND peer_embedding.product_version_id = peer.current_version_id
             AND peer_embedding.provider_code = source_embedding.provider_code
             AND peer_embedding.model_code = source_embedding.model_code
            WHERE source.tenant_id = {envelope.TenantId.Value}
              AND source.id = {productId} AND source.version = {envelope.ExpectedVersion}
              AND source.current_version_id = {envelope.Command.ProductVersionId}
              AND peer.current_version_id = {envelope.Command.PeerProductVersionId}
              AND source.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND peer.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND NOT EXISTS (
                  SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                  WHERE identity_link.tenant_id = source.tenant_id
                    AND identity_link.duplicate_product_id IN (source.id, peer.id))
            ORDER BY source_embedding.created_at_utc DESC,
                peer_embedding.created_at_utc DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<InventoryDuplicateCandidateRow?> FindSemanticCandidateAsync(
        CommandEnvelope<NominateInventorySemanticDuplicateCommand> envelope,
        Guid leftVersionId,
        Guid rightVersionId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryDuplicateCandidateRow>(
            FormattableStringFactory.Create(
                InventoryDuplicateQueries.Select +
                " WHERE candidate.tenant_id = {0} " +
                "AND candidate.left_product_version_id = {1} " +
                "AND candidate.right_product_version_id = {2} " +
                "AND candidate.method_code = {3}",
                envelope.TenantId.Value, leftVersionId, rightVersionId,
                MasterDataCodes.InventoryDuplicateMethods.SemanticVector))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record SemanticDuplicatePairRow(
        Guid SourceEmbeddingId,
        Guid PeerEmbeddingId,
        bool SourceFirst,
        string Provider,
        string Model,
        decimal Similarity);
}
