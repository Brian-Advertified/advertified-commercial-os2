using Advertified.Commercial.Application.EmailAutomation;

namespace Advertified.Commercial.Api.Tests;

internal sealed class StaleContextEmailProviderResolver(
    IEmailProviderClient provider) : IEmailProviderResolver, IDisposable
{
    private readonly ManualResetEventSlim blockedCallReached = new(false);
    private readonly ManualResetEventSlim releaseBlockedCall = new(false);
    private int calls;

    internal bool WaitForBlockedCall(TimeSpan timeout) => blockedCallReached.Wait(timeout);

    internal void ReleaseBlockedCall() => releaseBlockedCall.Set();

    public IEmailProviderClient Resolve(string providerCode)
    {
        if (!string.Equals(provider.ProviderCode, providerCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The configured email provider is unavailable.");
        }

        var call = Interlocked.Increment(ref calls);
        if (call == 3)
        {
            throw new InvalidOperationException(
                "Deterministic setup failure before delivery intent.");
        }
        if (call != 4)
        {
            return provider;
        }

        blockedCallReached.Set();
        if (!releaseBlockedCall.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The concurrent delivery test did not release its barrier.");
        }
        throw new InvalidOperationException(
            "Deterministic stale-context failure after another request persisted intent.");
    }

    public void Dispose()
    {
        blockedCallReached.Dispose();
        releaseBlockedCall.Dispose();
    }
}
