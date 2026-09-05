using System.Security.Cryptography;
using System.Text;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class SupplierClaimToken
{
    internal static (string Token, string Hash) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (token, Hash(token));
    }

    internal static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    internal static bool Matches(string token, string expectedHash)
    {
        var actual = Convert.FromHexString(Hash(token));
        var expected = Convert.FromHexString(expectedHash);
        return actual.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
