using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class EmailProviderResolverTests
{
    [Theory]
    [InlineData(EmailAutomationOptions.DisabledMode)]
    [InlineData(EmailAutomationOptions.ResendMode)]
    public void ProviderCannotBeResolvedOutsideItsActiveMode(string mode)
    {
        var provider = CreateProvider();
        var resolver = new EmailProviderResolver(
            [provider],
            Options.Create(new EmailAutomationOptions { Mode = mode }));

        Assert.Throws<EmailProviderUnavailableException>(() =>
            resolver.Resolve(MasterDataCodes.EmailProviders.Deterministic));
    }

    [Fact]
    public void ProviderResolvesOnlyInItsExactActiveMode()
    {
        var provider = CreateProvider();
        var resolver = new EmailProviderResolver(
            [provider],
            Options.Create(new EmailAutomationOptions
            {
                Mode = EmailAutomationOptions.DeterministicMode,
            }));

        Assert.Same(
            provider,
            resolver.Resolve(MasterDataCodes.EmailProviders.Deterministic));
    }

    private static DeterministicEmailProviderClient CreateProvider() => new(
        Options.Create(new EmailAutomationOptions()),
        TimeProvider.System);
}
