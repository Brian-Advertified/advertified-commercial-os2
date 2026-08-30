using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private async Task<CommandOutcome> CreateRfqOutcomeAsync(
        CommandEnvelope<CreateMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken)
    {
        ValidateRfq(envelope.Command);
        var listing = await FindPublishedListingVersionAsync(
            envelope.Command.ListingVersionId, cancellationToken)
            ?? throw new MarketplaceListingUnavailableException();
        if (listing.SupplierTenantId == envelope.TenantId.Value)
        {
            throw new ArgumentException("A buyer cannot request its own marketplace listing.");
        }
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_rfqs (
                id, buyer_tenant_id, supplier_tenant_id, listing_version_id,
                subject, requested_start, requested_end, quantity, due_at_utc,
                created_by, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {listing.SupplierTenantId},
                {envelope.Command.ListingVersionId},
                {Required(envelope.Command.Subject, 500, nameof(envelope.Command.Subject))},
                {envelope.Command.RequestedStart}, {envelope.Command.RequestedEnd},
                {envelope.Command.Quantity}, {envelope.Command.DueAtUtc},
                {envelope.ActorId.Value}, 1, {now}, {now})
            """, cancellationToken);
        var view = await LoadRfqViewAsync(id, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, id, view.Version,
            MasterDataReferences.CommercialResourceTypes.MarketplaceRfq,
            MasterDataReferences.CommercialActions.MarketplaceRfqCreated,
            MasterDataReferences.CommercialEventTypes.MarketplaceRfqCreated, now);
    }

    private async Task<CommandOutcome> SendRfqOutcomeAsync(
        Guid rfqId, CommandEnvelope<SendMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken)
    {
        _ = Required(envelope.Command.Reason, 1_000, nameof(envelope.Command.Reason));
        var rfq = await store.FindRfqAsync(
            rfqId, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace request access denied.");
        var now = timeProvider.GetUtcNow();
        if (rfq.BuyerTenantId != envelope.TenantId.Value ||
            rfq.Status != MasterDataCodes.MarketplaceRfqStatuses.Draft ||
            rfq.DueAtUtc <= now)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.marketplace_rfqs
            SET sent_by = {envelope.ActorId.Value}, sent_at_utc = {now},
                version = version + 1, updated_at_utc = {now}
            WHERE buyer_tenant_id = {envelope.TenantId.Value} AND id = {rfqId}
              AND sent_at_utc IS NULL AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var view = await LoadRfqViewAsync(rfqId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, rfqId, view.Version,
            MasterDataReferences.CommercialResourceTypes.MarketplaceRfq,
            MasterDataReferences.CommercialActions.MarketplaceRfqSent,
            MasterDataReferences.CommercialEventTypes.MarketplaceRfqSent, now);
    }

    private async Task<CommandOutcome> SubmitResponseOutcomeAsync(
        Guid rfqId, CommandEnvelope<SubmitMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken)
    {
        ValidateResponse(envelope.Command);
        await LockExchangeAsync(rfqId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var rfq = await store.FindRfqAsync(rfqId, now, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace request access denied.");
        if (rfq.SupplierTenantId != envelope.TenantId.Value ||
            rfq.Status != MasterDataCodes.MarketplaceRfqStatuses.Sent)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var responseId = Guid.NewGuid();
        var evidence = JsonSerializer.Serialize(
            envelope.Command.EvidenceReferences.Select(item => item.Trim()).ToArray());
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_supplier_responses (
                id, rfq_id, buyer_tenant_id, supplier_tenant_id, response_version,
                amount_minor, currency_code, availability_code, terms,
                valid_until_utc, evidence_references_json, submitted_by, submitted_at_utc)
            VALUES ({responseId}, {rfq.Id}, {rfq.BuyerTenantId}, {rfq.SupplierTenantId}, 1,
                {envelope.Command.AmountMinor}, {envelope.Command.Currency.Trim().ToUpperInvariant()},
                {envelope.Command.Availability.Trim().ToUpperInvariant()},
                {Required(envelope.Command.Terms, 5_000, nameof(envelope.Command.Terms))},
                {envelope.Command.ValidUntilUtc}, {evidence}::jsonb,
                {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        var view = await LoadRfqViewAsync(rfqId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, responseId, envelope.ExpectedVersion + 1,
            MasterDataReferences.CommercialResourceTypes.MarketplaceSupplierResponse,
            MasterDataReferences.CommercialActions.MarketplaceRfqResponseSubmitted,
            MasterDataReferences.CommercialEventTypes.MarketplaceResponseSubmitted, now);
    }

    private async Task<CommandOutcome> AcceptResponseOutcomeAsync(
        Guid responseId, CommandEnvelope<AcceptMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken)
    {
        await LockExchangeAsync(responseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var rfq = await store.FindRfqByResponseAsync(responseId, now, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace response access denied.");
        if (rfq.BuyerTenantId != envelope.TenantId.Value || !rfq.ResponseId.HasValue ||
            rfq.ResponseVersion != envelope.ExpectedVersion)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (rfq.ResponseValidUntilUtc <= now)
        {
            throw new MarketplaceResponseExpiredException();
        }
        if (rfq.Status != MasterDataCodes.MarketplaceRfqStatuses.Responded)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var reason = Required(envelope.Command.Reason, 1_000, nameof(envelope.Command.Reason));
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_response_acceptances (
                id, response_id, buyer_tenant_id, supplier_tenant_id,
                reason, accepted_by, accepted_at_utc)
            VALUES ({Guid.NewGuid()}, {responseId}, {rfq.BuyerTenantId},
                {rfq.SupplierTenantId}, {reason}, {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        var view = await LoadRfqViewAsync(rfq.Id, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, responseId, envelope.ExpectedVersion + 1,
            MasterDataReferences.CommercialResourceTypes.MarketplaceSupplierResponse,
            MasterDataReferences.CommercialActions.MarketplaceRfqResponseAccepted,
            MasterDataReferences.CommercialEventTypes.MarketplaceResponseAccepted, now);
    }

    private Task<MarketplaceListingRow?> FindPublishedListingVersionAsync(
        Guid versionId, CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<MarketplaceListingRow>(
            FormattableStringFactory.Create(
                MarketplaceRecordStore.ListingSelect +
                " WHERE listing.status_code = {0} AND version.id = {1}",
                MasterDataCodes.MarketplaceListingStatuses.Published, versionId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<MarketplaceRfqView> LoadRfqViewAsync(
        Guid rfqId, CancellationToken cancellationToken) =>
        (await store.FindRfqAsync(rfqId, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new InvalidOperationException("Marketplace request was not persisted."))
        .ToView();

    private Task<int> LockExchangeAsync(Guid rfqId, CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({rfqId.ToString("N")}, 0))",
            cancellationToken);

    private void ValidateRfq(CreateMarketplaceRfqCommand command)
    {
        var now = timeProvider.GetUtcNow();
        if (command.Quantity <= 0 || command.RequestedEnd < command.RequestedStart ||
            command.DueAtUtc <= now)
        {
            throw new ArgumentException("The marketplace request dates or quantity are invalid.");
        }
    }

    private void ValidateResponse(SubmitMarketplaceResponseCommand command)
    {
        if (command.AmountMinor < 0 || command.ValidUntilUtc <= timeProvider.GetUtcNow() ||
            command.EvidenceReferences.Count > 20 ||
            command.EvidenceReferences.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1_000))
        {
            throw new ArgumentException("The supplier response is invalid.");
        }
        _ = Required(command.Currency, 3, nameof(command.Currency));
        _ = Required(command.Availability, 100, nameof(command.Availability));
    }
}
