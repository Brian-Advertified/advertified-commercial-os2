using System.Security.Cryptography;
using System.Text;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class IntelligenceInputHash
{
    internal static string Combine(params string[] components)
    {
        var payload = string.Join('\u001f', components);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}
