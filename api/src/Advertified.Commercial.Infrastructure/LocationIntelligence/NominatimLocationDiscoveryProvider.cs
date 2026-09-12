using Advertified.Commercial.Application.LocationIntelligence;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class NominatimLocationDiscoveryProvider(
    HttpClient client,
    IOptions<LocationDiscoveryOptions> options,
    TimeProvider clock) : ILocationDiscoveryProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset nextRequestAt;
    private const int MaximumCacheEntries = 128;
    private const int MaximumResponseBytes = 256 * 1024;

    public void Dispose() => gate.Dispose();

    public async Task<IReadOnlyList<DiscoveredLocation>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length is < 2 or > 200)
            throw new ArgumentException("Supply a precise place and area.", nameof(query));
        options.Value.Validate();

        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            if (cache.TryGetValue(term, out var previous) && now - previous.At < TimeSpan.FromDays(1))
                return previous.Places;

            var wait = nextRequestAt - now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, clock, cancellationToken);
            nextRequestAt = clock.GetUtcNow().AddSeconds(1);

            var places = await FetchAsync(term, cancellationToken);
            if (cache.Count >= MaximumCacheEntries)
                cache.Remove(cache.MinBy(item => item.Value.At).Key);
            cache[term] = new(clock.GetUtcNow(), places);
            return places;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<DiscoveredLocation>> FetchAsync(
        string term,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(options.Value.Endpoint), "search");
        var url = $"{endpoint}?format=jsonv2&countrycodes=za&addressdetails=1&limit=10&q={Uri.EscapeDataString(term)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, timeout.Token);
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return NominatimLocationParser.Parse(json, clock.GetUtcNow());
    }

    private sealed record CacheEntry(
        DateTimeOffset At,
        IReadOnlyList<DiscoveredLocation> Places);
}
