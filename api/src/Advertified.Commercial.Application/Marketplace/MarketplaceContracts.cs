using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Marketplace;

public sealed record CreateMarketplaceListingCommand(
    Guid ProductId,
    string Terms);

public sealed record PublishMarketplaceListingCommand;

public sealed record ArchiveMarketplaceListingCommand(string Reason);

public sealed record CreateMarketplaceRfqCommand(
    Guid ListingVersionId,
    string Subject,
    DateOnly RequestedStart,
    DateOnly RequestedEnd,
    int Quantity,
    DateTimeOffset DueAtUtc);

public sealed record SendMarketplaceRfqCommand(string Reason);

public sealed record SubmitMarketplaceResponseCommand(
    long AmountMinor,
    string Currency,
    string Availability,
    string Terms,
    DateTimeOffset ValidUntilUtc,
    IReadOnlyList<string> EvidenceReferences);

public sealed record AcceptMarketplaceResponseCommand(string Reason);

public sealed record MarketplaceSearchQuery(
    string? Search,
    string? Channel,
    string? Geography,
    int PageSize,
    string? Cursor);

public sealed record MarketplaceRfqQuery(
    string? Status,
    int PageSize,
    string? Cursor);

public sealed record MarketplaceListingVersionView(
    Guid Id,
    int VersionNumber,
    Guid ProductVersionId,
    Guid RateId,
    Guid AvailabilityId,
    string SupplierName,
    string ProductName,
    string Channel,
    string ProductType,
    string Geography,
    string RateType,
    long AmountMinor,
    string Currency,
    string Availability,
    DateTimeOffset? AvailabilityValidUntilUtc,
    string Terms,
    Guid PublishedBy,
    DateTimeOffset PublishedAtUtc);

public sealed record MarketplaceListingView(
    Guid Id,
    Guid SupplierTenantId,
    Guid ProductId,
    string Status,
    MarketplaceListingVersionView? CurrentVersion,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record MarketplaceListingPage(
    IReadOnlyList<MarketplaceListingView> Items,
    string? NextCursor);

public sealed record MarketplaceResponseView(
    Guid Id,
    Guid RfqId,
    int ResponseVersion,
    long AmountMinor,
    string Currency,
    string Availability,
    string Terms,
    DateTimeOffset ValidUntilUtc,
    IReadOnlyList<string> EvidenceReferences,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAtUtc,
    Guid? AcceptedBy,
    DateTimeOffset? AcceptedAtUtc);

public sealed record MarketplaceRfqView(
    Guid Id,
    Guid BuyerTenantId,
    Guid SupplierTenantId,
    Guid ListingVersionId,
    string SupplierName,
    string ProductName,
    string Subject,
    DateOnly RequestedStart,
    DateOnly RequestedEnd,
    int Quantity,
    DateTimeOffset DueAtUtc,
    string Status,
    MarketplaceResponseView? Response,
    Guid CreatedBy,
    Guid? SentBy,
    DateTimeOffset? SentAtUtc,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record MarketplaceRfqPage(
    IReadOnlyList<MarketplaceRfqView> Items,
    string? NextCursor);

public interface IMarketplaceCommands
{
    Task<CommandResult<MarketplaceListingView>> CreateListingAsync(
        CommandEnvelope<CreateMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceListingView>> PublishListingAsync(
        Guid listingId,
        CommandEnvelope<PublishMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceListingView>> ArchiveListingAsync(
        Guid listingId,
        CommandEnvelope<ArchiveMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceRfqView>> CreateRfqAsync(
        CommandEnvelope<CreateMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceRfqView>> SendRfqAsync(
        Guid rfqId,
        CommandEnvelope<SendMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceRfqView>> SubmitResponseAsync(
        Guid rfqId,
        CommandEnvelope<SubmitMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MarketplaceRfqView>> AcceptResponseAsync(
        Guid responseId,
        CommandEnvelope<AcceptMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IMarketplaceReader
{
    Task<MarketplaceListingPage> SearchListingsAsync(
        ActorId actorId,
        TenantId tenantId,
        MarketplaceSearchQuery query,
        CancellationToken cancellationToken);

    Task<MarketplaceListingView> GetListingAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid listingId,
        CancellationToken cancellationToken);

    Task<MarketplaceRfqPage> ListRfqsAsync(
        ActorId actorId,
        TenantId tenantId,
        MarketplaceRfqQuery query,
        CancellationToken cancellationToken);

    Task<MarketplaceRfqView> GetRfqAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid rfqId,
        CancellationToken cancellationToken);
}

public sealed class MarketplaceListingUnavailableException : Exception;

public sealed class MarketplaceResponseExpiredException : Exception;
