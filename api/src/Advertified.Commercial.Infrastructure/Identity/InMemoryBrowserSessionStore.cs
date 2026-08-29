using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Identity;

namespace Advertified.Commercial.Infrastructure.Identity;

public sealed class InMemoryBrowserSessionStore(TimeProvider timeProvider)
    : IBrowserSessionStore
{
    private const int TokenBytes = 32;
    private const int MaximumSessions = 256;
    private readonly ConcurrentDictionary<string, BrowserSessionIdentity> sessions = new();

    public ValueTask<BrowserSessionHandle> CreateAsync(
        BrowserSessionIdentity identity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveExpiredSessions();
        if (sessions.Count >= MaximumSessions)
        {
            throw new InvalidOperationException("The local session store is full.");
        }

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));
        if (!sessions.TryAdd(Hash(token), identity))
        {
            throw new InvalidOperationException("The local session could not be created.");
        }

        return ValueTask.FromResult(new BrowserSessionHandle(token, identity));
    }

    public ValueTask<BrowserSessionIdentity?> ResolveAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Hash(token);
        if (!sessions.TryGetValue(key, out var identity))
        {
            return ValueTask.FromResult<BrowserSessionIdentity?>(null);
        }

        if (identity.ExpiresAtUtc > timeProvider.GetUtcNow())
        {
            return ValueTask.FromResult<BrowserSessionIdentity?>(identity);
        }

        sessions.TryRemove(key, out _);
        return ValueTask.FromResult<BrowserSessionIdentity?>(null);
    }

    public ValueTask InvalidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.TryRemove(Hash(token), out _);
        return ValueTask.CompletedTask;
    }

    private static string Hash(string token)
    {
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private void RemoveExpiredSessions()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in sessions.Where(item => item.Value.ExpiresAtUtc <= now))
        {
            sessions.TryRemove(item.Key, out _);
        }
    }
}
