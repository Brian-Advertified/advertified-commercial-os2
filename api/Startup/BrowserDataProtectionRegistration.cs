using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Advertified.Commercial.Api.Startup;

internal sealed class BrowserDataProtectionOptions
{
    internal const string SectionName = "Authentication:DataProtection";
    internal const string DefaultApplicationName = "Advertified.Commercial";

    public string ApplicationName { get; init; } = DefaultApplicationName;
    public string KeysDirectory { get; init; } = "";
    public string CertificatePath { get; init; } = "";
    public string CertificatePassword { get; init; } = "";
}

internal static class BrowserDataProtectionRegistration
{
    internal static void AddAdvertifiedDataProtection(
        this WebApplicationBuilder builder,
        ProcessRoleOptions processRole)
    {
        if (!processRole.RunsApi)
        {
            return;
        }

        var settings = builder.Configuration
            .GetSection(BrowserDataProtectionOptions.SectionName)
            .Get<BrowserDataProtectionOptions>() ??
            new BrowserDataProtectionOptions();
        var protection = builder.Services.AddDataProtection()
            .SetApplicationName(settings.ApplicationName);

        if (IsLocal(builder.Environment) ||
            builder.Configuration.GetValue<bool>("ReleaseSmoke:Enabled"))
        {
            return;
        }

        Validate(settings);
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            settings.CertificatePath,
            settings.CertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The browser data-protection certificate requires a private key.");
        }

        protection
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeysDirectory))
            .ProtectKeysWithCertificate(certificate);
    }

    private static bool IsLocal(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Test");

    private static void Validate(BrowserDataProtectionOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApplicationName))
        {
            throw new InvalidOperationException(
                "The browser data-protection application name is required.");
        }
        if (!Path.IsPathFullyQualified(settings.KeysDirectory) ||
            !Directory.Exists(settings.KeysDirectory))
        {
            throw new InvalidOperationException(
                "Production browser data-protection keys require an existing absolute directory.");
        }
        if (!Path.IsPathFullyQualified(settings.CertificatePath) ||
            !File.Exists(settings.CertificatePath) ||
            string.IsNullOrWhiteSpace(settings.CertificatePassword))
        {
            throw new InvalidOperationException(
                "Production browser data-protection keys require a password-protected certificate.");
        }
    }
}
