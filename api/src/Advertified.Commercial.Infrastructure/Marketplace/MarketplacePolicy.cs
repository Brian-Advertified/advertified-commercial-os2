using System.Text.Json;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Marketplace;

internal static class MarketplacePolicy
{
    private const int MaximumSearchLength = 200;
    private const int MaximumGeographyLength = 500;
    private const int MaximumTermsLength = 5_000;
    private const int MaximumReasonLength = 1_000;
    private const int MaximumEvidenceReferences = 20;
    private const int MaximumEvidenceReferenceLength = 1_000;

    private static readonly HashSet<string> ActiveChannels =
        LoadCodes(MasterDataCodes.Channels.Collection);
    private static readonly HashSet<string> ActiveCurrencies =
        LoadCodes(MasterDataCodes.Currencies.Collection);
    private static readonly HashSet<string> ActiveAvailabilityStatuses =
        LoadCodes(MasterDataCodes.AvailabilityStatuses.Collection);
    private static readonly HashSet<string> RfqStatuses =
    [
        MasterDataCodes.MarketplaceRfqStatuses.Draft,
        MasterDataCodes.MarketplaceRfqStatuses.Sent,
        MasterDataCodes.MarketplaceRfqStatuses.Responded,
        MasterDataCodes.MarketplaceRfqStatuses.Accepted,
        MasterDataCodes.MarketplaceRfqStatuses.Expired,
    ];

    internal static MarketplaceSearchFilters ValidateSearch(MarketplaceSearchQuery query)
    {
        var channel = Optional(query.Channel, 100, nameof(query))?.ToUpperInvariant();
        if (channel is not null && !ActiveChannels.Contains(channel))
        {
            throw new ArgumentException("Choose a supported media type.", nameof(query));
        }
        return new MarketplaceSearchFilters(
            Optional(query.Search, MaximumSearchLength, nameof(query)),
            channel,
            Optional(query.Geography, MaximumGeographyLength, nameof(query)));
    }

    internal static int ValidatePageSize(int value) => value is >= 1 and <= 100
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value));

    internal static string? ValidateRfqStatus(string? value)
    {
        var status = Optional(value, 100, nameof(value))?.ToUpperInvariant();
        return status is null || RfqStatuses.Contains(status)
            ? status
            : throw new ArgumentException("Choose a supported request status.", nameof(value));
    }

    internal static void ValidateRfq(CreateMarketplaceRfqCommand command, DateTimeOffset now)
    {
        if (command.Quantity <= 0 || command.RequestedEnd < command.RequestedStart ||
            command.DueAtUtc <= now)
        {
            throw new ArgumentException("The marketplace request dates or quantity are invalid.");
        }
    }

    internal static ValidatedMarketplaceResponse ValidateResponse(
        SubmitMarketplaceResponseCommand command,
        DateTimeOffset now)
    {
        if (command.AmountMinor < 0 || command.ValidUntilUtc <= now ||
            command.EvidenceReferences.Count > MaximumEvidenceReferences)
        {
            throw new ArgumentException("The supplier response is invalid.");
        }
        var currency = Required(command.Currency, 3, nameof(command)).ToUpperInvariant();
        var availability = Required(command.Availability, 100, nameof(command)).ToUpperInvariant();
        if (!ActiveCurrencies.Contains(currency) ||
            !ActiveAvailabilityStatuses.Contains(availability))
        {
            throw new ArgumentException("The supplier response uses an unsupported code.",
                nameof(command));
        }
        var evidence = command.EvidenceReferences
            .Select(item => Required(
                item, MaximumEvidenceReferenceLength, nameof(command)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new ValidatedMarketplaceResponse(
            currency,
            availability,
            Required(command.Terms, MaximumTermsLength, nameof(command)),
            JsonSerializer.Serialize(evidence));
    }

    internal static string RequiredSubject(string value) =>
        Required(value, 500, nameof(value));

    internal static string RequiredTerms(string value) =>
        Required(value, MaximumTermsLength, nameof(value));

    internal static string RequiredReason(string value) =>
        Required(value, MaximumReasonLength, nameof(value));

    private static string Required(string value, int maximum, string parameter)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximum)
        {
            throw new ArgumentException("A marketplace value is invalid.", parameter);
        }
        return normalized;
    }

    private static string? Optional(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maximum
            ? normalized
            : throw new ArgumentOutOfRangeException(parameter);
    }

    private static HashSet<string> LoadCodes(string collection) =>
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == collection).Items
            .Where(item => item.IsActive)
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
}

internal sealed record MarketplaceSearchFilters(
    string? Search,
    string? Channel,
    string? Geography);

internal sealed record ValidatedMarketplaceResponse(
    string Currency,
    string Availability,
    string Terms,
    string EvidenceJson);
