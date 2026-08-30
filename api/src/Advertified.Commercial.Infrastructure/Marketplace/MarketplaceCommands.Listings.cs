using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Marketplace;

public sealed partial class MarketplaceCommands
{
    private async Task<CommandOutcome> CreateListingOutcomeAsync(
        CommandEnvelope<CreateMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.FindProductSnapshotAsync(
            envelope.TenantId, envelope.Command.ProductId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        var existing = await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.marketplace_listings
            WHERE supplier_tenant_id = {envelope.TenantId.Value}
              AND product_id = {envelope.Command.ProductId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (existing != Guid.Empty)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var terms = Required(envelope.Command.Terms, 5_000, nameof(envelope.Command.Terms));
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_listings (
                id, supplier_tenant_id, product_id, status_code, terms,
                created_by, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {snapshot.ProductId},
                {MasterDataCodes.MarketplaceListingStatuses.Draft}, {terms},
                {envelope.ActorId.Value}, 1, {now}, {now})
            """, cancellationToken);
        var view = await LoadListingViewAsync(id, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, id, view.Version,
            MasterDataReferences.CommercialResourceTypes.MarketplaceListing,
            MasterDataReferences.CommercialActions.MarketplaceListingCreated,
            MasterDataReferences.CommercialEventTypes.MarketplaceListingCreated, now);
    }

    private async Task<CommandOutcome> PublishListingOutcomeAsync(
        Guid listingId, CommandEnvelope<PublishMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var listing = await store.FindListingAsync(listingId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace listing access denied.");
        if (listing.SupplierTenantId != envelope.TenantId.Value ||
            listing.Status == MasterDataCodes.MarketplaceListingStatuses.Archived)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var snapshot = await store.FindProductSnapshotAsync(
            envelope.TenantId, listing.ProductId, cancellationToken)
            ?? throw new MarketplaceListingUnavailableException();
        EnsureFresh(snapshot);
        var versionNumber = await store.DbContext.Database.SqlQuery<int>($"""
            SELECT (COALESCE(MAX(version_number), 0) + 1)::integer AS "Value"
            FROM commercial.marketplace_listing_versions
            WHERE supplier_tenant_id = {envelope.TenantId.Value}
              AND listing_id = {listingId}
            """).SingleAsync(cancellationToken);
        var versionId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await InsertListingVersionAsync(
            envelope, listing, snapshot, versionId, versionNumber, now, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.marketplace_listings
            SET current_version_id = {versionId},
                status_code = {MasterDataCodes.MarketplaceListingStatuses.Published},
                archived_reason = NULL, version = version + 1, updated_at_utc = {now}
            WHERE supplier_tenant_id = {envelope.TenantId.Value} AND id = {listingId}
              AND version = {envelope.ExpectedVersion}
              AND status_code <> {MasterDataCodes.MarketplaceListingStatuses.Archived}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var view = await LoadListingViewAsync(listingId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, listingId, view.Version,
            MasterDataReferences.CommercialResourceTypes.MarketplaceListing,
            MasterDataReferences.CommercialActions.MarketplaceListingPublished,
            MasterDataReferences.CommercialEventTypes.MarketplaceListingPublished, now);
    }

    private async Task<CommandOutcome> ArchiveListingOutcomeAsync(
        Guid listingId, CommandEnvelope<ArchiveMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var reason = Required(envelope.Command.Reason, 1_000, nameof(envelope.Command.Reason));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.marketplace_listings
            SET status_code = {MasterDataCodes.MarketplaceListingStatuses.Archived},
                archived_reason = {reason}, version = version + 1, updated_at_utc = {now}
            WHERE supplier_tenant_id = {envelope.TenantId.Value} AND id = {listingId}
              AND status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var view = await LoadListingViewAsync(listingId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, listingId, view.Version,
            MasterDataReferences.CommercialResourceTypes.MarketplaceListing,
            MasterDataReferences.CommercialActions.MarketplaceListingArchived,
            MasterDataReferences.CommercialEventTypes.MarketplaceListingArchived, now);
    }

    private Task<int> InsertListingVersionAsync(
        CommandEnvelope<PublishMarketplaceListingCommand> envelope,
        MarketplaceListingRow listing,
        MarketplaceProductSnapshotRow snapshot,
        Guid versionId,
        int versionNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_listing_versions (
                id, supplier_tenant_id, listing_id, version_number,
                product_version_id, rate_id, availability_id, supplier_name,
                product_name, channel_code, product_type_code, geography,
                rate_type_code, amount_minor, currency_code, availability_code,
                availability_valid_until_utc, terms, published_by, published_at_utc)
            VALUES ({versionId}, {envelope.TenantId.Value}, {listing.Id}, {versionNumber},
                {snapshot.ProductVersionId}, {snapshot.RateId}, {snapshot.AvailabilityId},
                {snapshot.SupplierName}, {snapshot.ProductName}, {snapshot.Channel},
                {snapshot.ProductType}, {snapshot.Geography}, {snapshot.RateType},
                {snapshot.AmountMinor}, {snapshot.Currency}, {snapshot.Availability},
                {snapshot.AvailabilityValidUntilUtc}, {listing.ListingTerms},
                {envelope.ActorId.Value}, {now})
            """, cancellationToken);

    private async Task<MarketplaceListingView> LoadListingViewAsync(
        Guid listingId, CancellationToken cancellationToken) =>
        (await store.FindListingAsync(listingId, cancellationToken)
            ?? throw new InvalidOperationException("Marketplace listing was not persisted."))
        .ToView();

    private void EnsureFresh(MarketplaceProductSnapshotRow snapshot)
    {
        var now = timeProvider.GetUtcNow();
        if (snapshot.Availability != MasterDataCodes.AvailabilityStatuses.Available ||
            snapshot.AvailabilityValidUntilUtc.HasValue &&
            snapshot.AvailabilityValidUntilUtc.Value <= now ||
            snapshot.RateEffectiveTo.HasValue && snapshot.RateEffectiveTo.Value < DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new MarketplaceListingUnavailableException();
        }
    }
}
