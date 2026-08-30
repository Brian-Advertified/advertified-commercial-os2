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
    private async Task<CommandOutcome> PublishOutcomeAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        EnsurePublicationAllowed(source, envelope);
        var candidates = await store.ListCandidatesAsync(
            envelope.TenantId, importId, cancellationToken);
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var approved = PrepareApprovedCandidates(candidates, codes);
        await InventoryPublicationPersistence.LockSupplierAsync(
            store.DbContext, envelope.TenantId, source.SupplierId, cancellationToken);
        var products = await InventoryPublicationPersistence.LoadProductsAsync(
            store.DbContext, envelope.TenantId, source.SupplierId,
            approved.Select(item => item.ProductCode).ToArray(), cancellationToken);
        var nextVersions = await InventoryPublicationPersistence.LoadNextVersionsAsync(
            store.DbContext, envelope.TenantId,
            products.Select(item => item.Id).ToArray(), cancellationToken);
        var publications = PreparePublications(
            source, approved, products, nextVersions);
        var now = timeProvider.GetUtcNow();
        await InventoryPublicationPersistence.PersistAsync(
            store.DbContext, envelope.TenantId, source.SupplierId, source.Id,
            envelope.ActorId.Value, now, publications, cancellationToken);
        await CompletePublicationAsync(envelope, source, now, cancellationToken);
        var updated = await store.FindImportAsync(
            envelope.TenantId, importId, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, importId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryPublished,
            MasterDataReferences.CommercialEventTypes.InventoryPublished, now);
    }

    private static void EnsurePublicationAllowed(
        InventoryImportRow source,
        CommandEnvelope<PublishInventoryImportCommand> envelope)
    {
        if (source.CreatedBy == envelope.ActorId.Value)
        {
            throw new ApprovalRequiredException();
        }
        if (source.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired ||
            source.ProtectedObjectKey is null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (source.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
    }

    private static ApprovedInventoryCandidate[] PrepareApprovedCandidates(
        List<InventoryCandidateRow> candidates,
        InventoryCodeSets codes)
    {
        if (candidates.Count == 0 || candidates.Any(
                item => item.Status == MasterDataCodes.LifecycleStatuses.ReviewRequired))
        {
            throw new InventoryPublishBlockedException();
        }
        var approved = candidates
            .Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Approved)
            .Select(item => PrepareApprovedCandidate(item, codes))
            .ToArray();
        if (approved.Length == 0 || approved
            .GroupBy(item => item.ProductCode, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new InventoryPublishBlockedException();
        }
        return approved;
    }

    private static ApprovedInventoryCandidate PrepareApprovedCandidate(
        InventoryCandidateRow candidate,
        InventoryCodeSets codes)
    {
        var values = JsonSerializer.Deserialize<InventoryCandidateValues>(
            candidate.ValuesJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored inventory values are invalid.");
        if (InventoryCandidateValidator.Validate(values, codes).Any(issue => issue.IsBlocking))
        {
            throw new InventoryPublishBlockedException();
        }
        return new ApprovedInventoryCandidate(
            candidate, values, Required(values.ProductCode));
    }

    private static PreparedInventoryPublication[] PreparePublications(
        InventoryImportRow source,
        IReadOnlyList<ApprovedInventoryCandidate> candidates,
        IReadOnlyList<ExistingInventoryProductRow> products,
        IReadOnlyList<InventoryProductVersionNumberRow> nextVersions)
    {
        var byCode = products.ToDictionary(item => item.ProductCode, StringComparer.Ordinal);
        var versions = nextVersions.ToDictionary(item => item.ProductId);
        return candidates.Select(item =>
        {
            var exists = byCode.TryGetValue(item.ProductCode, out var product);
            var productId = exists ? product!.Id : Guid.NewGuid();
            var versionNumber = exists && versions.TryGetValue(productId, out var number)
                ? number.NextVersionNumber : 1;
            var values = item.Values;
            return new PreparedInventoryPublication(
                productId, item.ProductCode, !exists, Guid.NewGuid(), versionNumber,
                item.Candidate.Id, Required(values.Name), Required(values.Channel),
                Required(values.ProductType), Required(values.Geography), values.Address,
                values.Latitude, values.Longitude,
                JsonSerializer.Serialize(
                    values.Extension ?? new Dictionary<string, string>(),
                    InventoryRowMapper.StoredJson),
                Guid.NewGuid(), Required(values.RateType), Required(values.Currency),
                values.RateAmountMinor ?? throw new InventoryPublishBlockedException(),
                Guid.NewGuid(), Required(values.Availability), Guid.NewGuid(),
                AssetType(source, values), Required(source.ProtectedObjectKey),
                source.SourceHash, source.DeclaredMediaType, item.Candidate.SourceLocator);
        }).ToArray();
    }

    private static string AssetType(
        InventoryImportRow source,
        InventoryCandidateValues values) => source.DocumentClass switch
    {
        MasterDataCodes.DocumentClasses.Png or MasterDataCodes.DocumentClasses.Jpeg
            when values.Channel == MasterDataCodes.Channels.Ooh =>
            MasterDataCodes.AssetTypes.OohPhoto,
        MasterDataCodes.DocumentClasses.Png or MasterDataCodes.DocumentClasses.Jpeg =>
            MasterDataCodes.AssetTypes.ProductImage,
        _ => MasterDataCodes.AssetTypes.RateCard,
    };

    private static string Required(string? value) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InventoryPublishBlockedException();

    private async Task CompletePublicationAsync(
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {source.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await RecordStepAsync(
            envelope.TenantId, source.Id,
            MasterDataCodes.InventoryImportStepTypes.Publication,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
    }
}

internal sealed record ApprovedInventoryCandidate(
    InventoryCandidateRow Candidate,
    InventoryCandidateValues Values,
    string ProductCode);
