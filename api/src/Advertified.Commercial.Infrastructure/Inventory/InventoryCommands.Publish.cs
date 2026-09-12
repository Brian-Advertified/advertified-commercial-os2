using System.Text.Json;

using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> PublishOutcomeAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        EnsurePublicationAllowed(source, envelope);
        var supplierId = source.SupplierId!.Value;
        var candidates = await store.ListCandidatesAsync(
            envelope.TenantId, importId, cancellationToken);
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var acceptance = await VerifyRetainedAcceptanceAsync(source, candidates, codes, cancellationToken);
        var approved = PrepareApprovedCandidates(candidates, codes);
        await InventoryPublicationPersistence.LockSupplierAsync(
            store.DbContext, envelope.TenantId, supplierId, cancellationToken);
        var supplier = InventorySupplierPublication.Prepare(approved);
        var productCodes = approved.Select(item => item.ProductCode)
            .Concat(approved.SelectMany(item =>
                item.Values.Package?.ComponentProductCodes ?? []))
            .Distinct(StringComparer.Ordinal).ToArray();
        var products = await InventoryPublicationPersistence.LoadProductsAsync(
            store.DbContext, envelope.TenantId, supplierId,
            productCodes, cancellationToken);
        var nextVersions = await InventoryPublicationPersistence.LoadNextVersionsAsync(
            store.DbContext, envelope.TenantId,
            products.Select(item => item.Id).ToArray(), cancellationToken);
        var publications = PreparePublications(
            source, approved, products, nextVersions);
        var now = timeProvider.GetUtcNow();
        var release = await InventorySupplierReleasePublication.BeginAsync(
            store.DbContext, envelope.TenantId, supplierId, source.Id,
            source.ReplacementMode, envelope.ActorId.Value, now, cancellationToken);
        await InventorySupplierPublication.PersistAsync(
            store.DbContext, envelope.TenantId, supplierId, source.Id,
            envelope.ActorId.Value, now, supplier, cancellationToken);
        await InventoryPublicationPersistence.PersistAsync(
            store.DbContext, envelope.TenantId, supplierId, source.Id,
            release.ReleaseId, envelope.ActorId.Value, now, publications,
            cancellationToken);
        var impactCount = await InventorySupplierReleasePublication.CompleteAsync(
            store.DbContext, envelope.TenantId, supplierId, source.Id,
            envelope.ActorId.Value, now, release, cancellationToken);
        await CompletePublicationAsync(
            envelope, source, release.ReleaseId, now, cancellationToken);
        var updated = await store.FindImportAsync(
            envelope.TenantId, importId, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        var outcome = OpportunityCommandSupport.Outcome(
            envelope, view, importId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryPublished,
            MasterDataReferences.CommercialEventTypes.InventoryPublished, now);
        return AddInventoryReleaseConsequences(
            outcome, envelope, release, impactCount, now, acceptance);
    }

    private async Task<IReadOnlyList<CandidateAcceptanceAudit>> VerifyRetainedAcceptanceAsync(
        InventoryImportRow source,
        IReadOnlyList<InventoryCandidateRow> candidates,
        InventoryCodeSets codes,
        CancellationToken cancellationToken)
    {
        var decisions = new List<CandidateAcceptanceAudit>();
        foreach (var group in candidates
            .Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Approved)
            .GroupBy(item => item.ProjectionId))
        {
            var artifact = await InventoryRetainedAcceptance.LoadAsync(
                store.DbContext,
                new TenantId(source.TenantId),
                group.First().Id,
                cancellationToken);
            if (!InventoryRetainedSchemaProjection.HasRetainedLineage(artifact.Extraction().Document))
                throw new InventoryPublishBlockedException();
            var evaluated = InventoryRetainedAcceptance.Evaluate(
                    artifact,
                    source,
                    codes,
                    timeProvider.GetUtcNow())
                .ToDictionary(item => item.RowNumber);
            foreach (var row in group)
            {
                InventoryRetainedAcceptance.EnsureMatches(
                    row,
                    evaluated.TryGetValue(row.RowNumber, out var value) ? [value] : []);
                decisions.Add(new CandidateAcceptanceAudit(
                    row.Id,
                    InventoryAcceptancePolicy.Read(evaluated[row.RowNumber].Values)
                        ?? throw new InventoryPublishBlockedException()));
            }
        }
        return decisions;
    }

    private sealed record CandidateAcceptanceAudit(
        Guid CandidateId,
        InventoryAcceptanceEvaluation Evaluation);

    private static void EnsurePublicationAllowed(
        InventoryImportRow source,
        CommandEnvelope<PublishInventoryImportCommand> envelope)
    {
        if (source.CreatedBy == envelope.ActorId.Value)
        {
            throw new ApprovalRequiredException();
        }
        if (source.SupplierId is null ||
            source.SupplierResolutionStatus !=
                MasterDataCodes.InventorySupplierResolutionStatuses.Resolved)
        {
            throw new SupplierIdentityAmbiguousException();
        }
        if (source.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired ||
            source.ProtectedObjectKey is null ||
            source.FailureCode is not null ||
            source.PublishedReleaseId is not null ||
            source.ReplacementMode !=
                MasterDataCodes.InventoryReplacementModes.FullReplacement)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (source.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
    }

    private static CommandOutcome AddInventoryReleaseConsequences(
        CommandOutcome outcome,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryReleaseCutover release,
        int proposalImpactCount,
        DateTimeOffset now,
        IReadOnlyList<CandidateAcceptanceAudit> acceptance)
    {
        var publicationEvidence = JsonSerializer.SerializeToElement(new
        {
            releaseId = release.ReleaseId,
            release.VersionNumber,
            release.PreviousReleaseId,
            proposalImpactCount,
            acceptanceEvaluations = acceptance,
        });
        var published = CommandOutcomeFactory.Create(
            envelope,
            publicationEvidence,
            release.ReleaseId,
            1,
            MasterDataReferences.CommercialResourceTypes.InventorySupplierRelease,
            MasterDataReferences.CommercialActions.InventoryReleasePublished,
            MasterDataReferences.CommercialEventTypes.InventorySupplierReleasePublished,
            now,
            auditMetadata: publicationEvidence);
        var result = outcome.WithAdditional(published.Audit, published.Outbox);
        if (!release.PreviousReleaseId.HasValue ||
            !release.PreviousAggregateVersion.HasValue)
        {
            return result;
        }
        var superseded = CommandOutcomeFactory.Create(
            envelope,
            new
            {
                releaseId = release.PreviousReleaseId.Value,
                supersededByReleaseId = release.ReleaseId,
            },
            release.PreviousReleaseId.Value,
            release.PreviousAggregateVersion.Value,
            MasterDataReferences.CommercialResourceTypes.InventorySupplierRelease,
            MasterDataReferences.CommercialActions.InventoryReleaseSuperseded,
            MasterDataReferences.CommercialEventTypes.InventorySupplierReleaseSuperseded,
            now);
        return result.WithAdditional(superseded.Audit, superseded.Outbox);
    }

}