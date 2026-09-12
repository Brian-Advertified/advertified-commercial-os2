using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.LocationIntelligence;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class OverpassPoiDiscoveryProvider(
    HttpClient client,
    IOptions<PoiDiscoveryOptions> options,
    TimeProvider clock) : IPoiDiscoveryProvider, IDisposable
{
    private const int MaximumLimit = 20;
    private const int MaximumResponseBytes = 512 * 1024;
    private const double MaximumBoundingBoxAreaKm2 = 200_000;
    private const double MaximumLatitudeSpanDegrees = 7.5;
    private const double MaximumLongitudeSpanDegrees = 8.0;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> cache = new(StringComparer.Ordinal);
    private DateTimeOffset nextRequestAt;

    public void Dispose() => gate.Dispose();

    public async Task<IReadOnlyList<DiscoveredPoi>> DiscoverAsync(
        PoiDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(request), "POI discovery limit is outside the governed range.");

        options.Value.Validate();
        var category = OpenStreetMapPoiCategoryCatalog.Resolve(request.PoiCategory);
        var bounds = ValidateScope(request.Anchor);
        var key = CacheKey(request.PoiCategory, request.Anchor.Id, bounds, request.Limit);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            if (cache.TryGetValue(key, out var cached) && now - cached.At < TimeSpan.FromHours(6))
                return cached.Places;

            var wait = nextRequestAt - now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, clock, cancellationToken);
            nextRequestAt = clock.GetUtcNow().AddSeconds(1);

            var places = await FetchAsync(category, request.Anchor, bounds, request.Limit, cancellationToken);
            if (cache.Count >= 128)
                cache.Remove(cache.MinBy(item => item.Value.At).Key);
            cache[key] = new CacheEntry(clock.GetUtcNow(), places);
            return places;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<DiscoveredPoi>> FetchAsync(
        OpenStreetMapPoiCategoryDefinition category,
        DiscoveredLocation anchor,
        LocationBounds bounds,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(category, bounds, limit);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["data"] = query,
                }),
            };
            request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransient(response.StatusCode) && attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), clock, cancellationToken);
                        continue;
                    }
                    if (IsTransient(response.StatusCode))
                        throw new PoiDiscoveryProviderException(
                            $"The POI provider was temporarily unavailable ({(int)response.StatusCode}).");
                    response.EnsureSuccessStatusCode();
                }

                await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, timeout.Token);
                var json = await response.Content.ReadAsStringAsync(timeout.Token);
                try
                {
                    return OverpassPoiParser.Parse(json, anchor, limit);
                }
                catch (JsonException exception)
                {
                    throw new PoiDiscoveryProviderException(
                        "The POI provider returned an unusable response.", exception);
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), clock, cancellationToken);
                    continue;
                }
                throw new PoiDiscoveryProviderException(
                    "The POI provider timed out before bounded discovery completed.", exception);
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null)
            {
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), clock, cancellationToken);
                    continue;
                }
                throw new PoiDiscoveryProviderException(
                    "The POI provider could not be reached.", exception);
            }
        }

        throw new PoiDiscoveryProviderException("The POI provider did not complete bounded discovery.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static string BuildQuery(
        OpenStreetMapPoiCategoryDefinition category,
        LocationBounds bounds,
        int limit)
    {
        var bbox = string.Join(",",
            bounds.South.ToString(CultureInfo.InvariantCulture),
            bounds.West.ToString(CultureInfo.InvariantCulture),
            bounds.North.ToString(CultureInfo.InvariantCulture),
            bounds.East.ToString(CultureInfo.InvariantCulture));
        var filters = string.Concat(category.Tags.Select(item =>
            $"[\"{Escape(item.Key)}\"=\"{Escape(item.Value)}\"]"));
        var builder = new StringBuilder("[out:json][timeout:10];(");
        foreach (var elementType in new[] { "node", "way", "relation" })
            builder.Append(elementType).Append(filters).Append('(').Append(bbox).Append(");");
        builder.Append(");out center qt ").Append(limit).Append(';');
        return builder.ToString();
    }

    private static LocationBounds ValidateScope(DiscoveredLocation anchor)
    {
        var bounds = anchor.Bounds
            ?? throw new PoiDiscoveryScopeException(
                PoiDiscoveryScopeFailure.NoBounds,
                $"{anchor.Name} has no verified provider bounds, so POI enumeration was not attempted.");
        var latitudeSpan = (double)(bounds.North - bounds.South);
        var longitudeSpan = (double)(bounds.East - bounds.West);
        if (latitudeSpan <= 0 || longitudeSpan <= 0)
            throw new PoiDiscoveryScopeException(
                PoiDiscoveryScopeFailure.InvalidBounds,
                $"{anchor.Name} has unusable provider bounds.");

        var midLatitudeRadians = (double)((bounds.North + bounds.South) / 2m) * Math.PI / 180d;
        var areaKm2 = latitudeSpan * 111.32d * longitudeSpan * 111.32d * Math.Cos(midLatitudeRadians);
        if (latitudeSpan > MaximumLatitudeSpanDegrees ||
            longitudeSpan > MaximumLongitudeSpanDegrees ||
            areaKm2 > MaximumBoundingBoxAreaKm2)
            throw new PoiDiscoveryScopeException(
                PoiDiscoveryScopeFailure.TooBroad,
                $"{anchor.Name} is too broad for bounded POI enumeration and must be narrowed to governed sub-geographies.");

        if (bounds.South < -35.2m || bounds.North > -21.7m ||
            bounds.West < 15.5m || bounds.East > 33.5m)
            throw new PoiDiscoveryScopeException(
                PoiDiscoveryScopeFailure.OutsideSouthAfrica,
                $"{anchor.Name} resolved outside the governed South Africa discovery boundary.");

        return bounds;
    }

    private static string CacheKey(
        string category,
        string anchorId,
        LocationBounds bounds,
        int limit) => string.Join('|',
            category,
            anchorId,
            bounds.South.ToString(CultureInfo.InvariantCulture),
            bounds.North.ToString(CultureInfo.InvariantCulture),
            bounds.West.ToString(CultureInfo.InvariantCulture),
            bounds.East.ToString(CultureInfo.InvariantCulture),
            limit.ToString(CultureInfo.InvariantCulture));

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed record CacheEntry(DateTimeOffset At, IReadOnlyList<DiscoveredPoi> Places);
}

internal enum PoiDiscoveryScopeFailure
{
    NoBounds,
    OutsideSouthAfrica,
    InvalidBounds,
    TooBroad,
}

internal sealed class PoiDiscoveryScopeException(
    PoiDiscoveryScopeFailure failure,
    string message) : InvalidOperationException(message)
{
    internal PoiDiscoveryScopeFailure Failure { get; } = failure;
}

internal sealed class PoiDiscoveryProviderException : InvalidOperationException
{
    internal PoiDiscoveryProviderException(string message) : base(message)
    {
    }

    internal PoiDiscoveryProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
