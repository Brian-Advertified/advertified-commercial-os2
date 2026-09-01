using Advertified.Commercial.Application.EmailAutomation;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailProviderResolver(
    IEnumerable<IEmailProviderClient> clients,
    IOptions<EmailAutomationOptions> options) : IEmailProviderResolver
{
    private readonly Dictionary<string, IEmailProviderClient> providers =
        clients.ToDictionary(client => client.ProviderCode, StringComparer.Ordinal);

    public IEmailProviderClient Resolve(string providerCode)
    {
        var normalized = providerCode.Trim().ToUpperInvariant();
        return options.Value.IsProviderEnabled(normalized) &&
            providers.TryGetValue(normalized, out var provider)
            ? provider
            : throw new EmailProviderUnavailableException();
    }
}
