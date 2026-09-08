using Advertified.Commercial.Application.Inventory;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Singleton registration serializes requests across users in this API process.
// Public service must run on ONE application instance; scaled deployment needs its own provider.
public sealed class NominatimPlaceDiscoveryProvider(
    HttpClient client, IOptions<PlaceDiscoveryOptions> options, TimeProvider clock) : IPlaceDiscoveryProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset nextRequestAt;
    private const int MaximumCacheEntries = 128;
    private const int MaximumResponseBytes = 256 * 1024;

    public void Dispose() => gate.Dispose();

    public async Task<PlaceDiscoveryResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length is < 2 or > 200) throw new ArgumentException("Supply a precise place and area.", nameof(query));
        if (!options.Value.IsConfigured()) return new(false, []);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            if (cache.TryGetValue(term, out var previous) && now - previous.At < TimeSpan.FromDays(1))
                return new(true, previous.Places);
            var wait = nextRequestAt - now;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, cancellationToken);
            nextRequestAt = clock.GetUtcNow().AddSeconds(1);
            var places = await FetchAsync(term, cancellationToken);
            if (cache.Count >= MaximumCacheEntries) cache.Remove(cache.MinBy(item => item.Value.At).Key);
            cache[term] = new(clock.GetUtcNow(), places);
            return new(true, places);
        }
        finally { gate.Release(); }
    }

    private async Task<IReadOnlyList<DiscoveredPlace>> FetchAsync(string term, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(options.Value.Endpoint), "search");
        var url = $"{endpoint}?format=jsonv2&countrycodes=za&layer=poi&addressdetails=1&limit=10&q={Uri.EscapeDataString(term)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, timeout.Token);
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return NominatimPlaceParser.Parse(json, clock.GetUtcNow());
    }

    private sealed record CacheEntry(DateTimeOffset At, IReadOnlyList<DiscoveredPlace> Places);
}
