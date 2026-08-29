using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Identity;

public sealed record BrowserSessionIdentity(
    UserId UserId,
    ActorId ActorId,
    bool IsServiceIdentity,
    DateTimeOffset ExpiresAtUtc);

public sealed record BrowserSessionHandle(
    string Token,
    BrowserSessionIdentity Identity);

public interface IBrowserSessionStore
{
    ValueTask<BrowserSessionHandle> CreateAsync(
        BrowserSessionIdentity identity,
        CancellationToken cancellationToken);

    ValueTask<BrowserSessionIdentity?> ResolveAsync(
        string token,
        CancellationToken cancellationToken);

    ValueTask InvalidateAsync(
        string token,
        CancellationToken cancellationToken);
}
