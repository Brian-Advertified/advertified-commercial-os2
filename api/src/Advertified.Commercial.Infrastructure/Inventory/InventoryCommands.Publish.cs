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

    private static ApprovedInventoryCandidate[] PrepareApprovedCandidates(
        List<InventoryCandidateRow> candidates,
        InventoryCodeSets codes)
    {
        if (candidates.Count == 0 || candidates.Any(item =>
                item.Status is not (MasterDataCodes.LifecycleStatuses.Approved or
                    MasterDataCodes.LifecycleStatuses.Rejected)))
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
        if (InventoryPendingSupplierValidationPolicy.Apply(
                values,
                InventoryCandidateValidator.Validate(values, codes))
            .Any(issue => issue.IsBlocking))
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
        var knownCodes = candidates.Select(item => item.ProductCode)
            .Concat(products.Select(item => item.ProductCode))
            .ToHashSet(StringComparer.Ordinal);
        return candidates.Select(item => PreparePublication(
            source, item, byCode, versions, knownCodes)).ToArray();
    }

    private static PreparedInventoryPublication PreparePublication(
        InventoryImportRow source,
        ApprovedInventoryCandidate item,
        Dictionary<string, ExistingInventoryProductRow> byCode,
        Dictionary<Guid, InventoryProductVersionNumberRow> versions,
        HashSet<string> knownCodes)
    {
        var exists = byCode.TryGetValue(item.ProductCode, out var product);
        var productId = exists ? product!.Id : Guid.NewGuid();
        var versionNumber = exists && versions.TryGetValue(productId, out var number)
            ? number.NextVersionNumber : 1;
        var values = item.Values;
        ValidatePackage(values.Package, knownCodes);
        var asset = PrepareAsset(source, values);
        var package = PreparePackage(values.Package);
        var spatial = values.Spatial;
        return new PreparedInventoryPublication(
            productId, item.ProductCode, !exists, Guid.NewGuid(), versionNumber,
            item.Candidate.Id, Required(values.Name), Required(values.Channel),
            Required(values.ProductType), Required(values.Geography), values.Address,
            values.Latitude, values.Longitude, values.Description,
            WriteRequired(values.Extension ?? new Dictionary<string, string>()),
            WriteOptional(values.AudienceProfile), WriteOptional(values.Deliverable),
            WriteOptional(spatial),
            spatial?.CoverageGeoJson, spatial?.CatchmentGeoJson,
            spatial?.RouteGeoJson, spatial?.DirectionGeoJson,
            Guid.NewGuid(), Required(values.RateType), Required(values.Currency),
            values.RateAmountMinor ?? throw new InventoryPublishBlockedException(),
            values.CommercialTerms?.RateValidFrom, values.CommercialTerms?.RateValidTo,
            values.CommercialTerms?.VatTreatment, WriteOptional(values.CommercialTerms),
            Guid.NewGuid(), Required(values.Availability), asset.Id, asset.Type,
            asset.ObjectKey, asset.Hash, asset.MediaType, item.Candidate.SourceLocator,
            package.Id, package.Code, package.Name, package.ComponentsJson,
            package.DiscountRule, package.ConditionsJson);
    }

    private static PreparedAsset PrepareAsset(
        InventoryImportRow source,
        InventoryCandidateValues values)
    {
        var type = AssetType(source, values);
        return type is null
            ? new(null, null, null, null, null)
            : new(Guid.NewGuid(), type, source.ProtectedObjectKey,
                source.SourceHash, source.DeclaredMediaType);
    }

    private static PreparedPackage PreparePackage(InventoryPackageValues? package) =>
        package is null
            ? new(null, null, null, null, null, null)
            : new(Guid.NewGuid(), package.PackageCode, package.PackageName,
                WriteRequired(package.ComponentProductCodes), package.DiscountRule,
                WriteRequired(package.Conditions));

    private static void ValidatePackage(
        InventoryPackageValues? package,
        HashSet<string> knownCodes)
    {
        if (package is null) return;
        if (string.IsNullOrWhiteSpace(package.PackageCode) ||
            string.IsNullOrWhiteSpace(package.PackageName) ||
            package.ComponentProductCodes.Count == 0 ||
            package.ComponentProductCodes.Any(code => !knownCodes.Contains(code)))
        {
            throw new InventoryPublishBlockedException();
        }
    }

    private static string? AssetType(
        InventoryImportRow source,
        InventoryCandidateValues values) => source.DocumentClass switch
    {
        MasterDataCodes.DocumentClasses.Png or MasterDataCodes.DocumentClasses.Jpeg
            when values.Channel == MasterDataCodes.Channels.Ooh =>
            MasterDataCodes.AssetTypes.OohPhoto,
        MasterDataCodes.DocumentClasses.Png or MasterDataCodes.DocumentClasses.Jpeg =>
            MasterDataCodes.AssetTypes.ProductImage,
        _ => null,
    };

    private static string Required(string? value) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InventoryPublishBlockedException();

    private static string WriteRequired<T>(T value) =>
        JsonSerializer.Serialize(value, InventoryRowMapper.StoredJson);

    private static string? WriteOptional<T>(T? value) where T : class =>
        value is null ? null : WriteRequired(value);

    private async Task CompletePublicationAsync(
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        InventoryImportRow source,
        Guid releaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                published_release_id = {releaseId},
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
            MasterDataCodes.LifecycleStatuses.Completed,
            now, cancellationToken);
    }
}

internal sealed record PreparedAsset(
    Guid? Id, string? Type, string? ObjectKey, string? Hash, string? MediaType);

internal sealed record PreparedPackage(
    Guid? Id, string? Code, string? Name, string? ComponentsJson,
    string? DiscountRule, string? ConditionsJson);

internal sealed record ApprovedInventoryCandidate(
    InventoryCandidateRow Candidate,
    InventoryCandidateValues Values,
    string ProductCode);
