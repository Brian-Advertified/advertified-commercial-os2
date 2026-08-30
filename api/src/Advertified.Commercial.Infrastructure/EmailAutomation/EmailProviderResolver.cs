using Advertified.Commercial.Application.EmailAutomation;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailProviderResolver(
    IEnumerable<IEmailProviderClient> clients) : IEmailProviderResolver
{
    private readonly Dictionary<string, IEmailProviderClient> providers =
        clients.ToDictionary(client => client.ProviderCode, StringComparer.Ordinal);

    public IEmailProviderClient Resolve(string providerCode)
    {
        var normalized = providerCode.Trim().ToUpperInvariant();
        return providers.TryGetValue(normalized, out var provider)
            ? provider
            : throw new InvalidOperationException(
                "The configured email provider is unavailable.");
    }
}
