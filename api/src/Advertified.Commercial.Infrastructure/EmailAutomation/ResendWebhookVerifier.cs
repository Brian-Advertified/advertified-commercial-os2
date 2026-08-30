using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

internal static class ResendWebhookVerifier
{
    private const string SecretPrefix = "whsec_";
    private const string SignatureVersion = "v1";

    internal static bool Verify(
        string rawPayload,
        string messageId,
        string timestamp,
        string signatureHeader,
        string secret,
        TimeSpan tolerance,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(rawPayload) ||
            string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(signatureHeader) ||
            !secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture,
                out var timestampSeconds))
        {
            return false;
        }

        var sentAt = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if ((now - sentAt).Duration() > tolerance)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(PadBase64(secret[SecretPrefix.Length..]));
        }
        catch (FormatException)
        {
            return false;
        }

        var signedContent = Encoding.UTF8.GetBytes(
            string.Concat(messageId, ".", timestamp, ".", rawPayload));
        var expected = HMACSHA256.HashData(key, signedContent);
        foreach (var candidate in signatureHeader.Split(' ',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = candidate.IndexOf(',', StringComparison.Ordinal);
            if (separator <= 0 ||
                !candidate.AsSpan(0, separator).SequenceEqual(SignatureVersion))
            {
                continue;
            }
            try
            {
                var supplied = Convert.FromBase64String(PadBase64(candidate[(separator + 1)..]));
                if (supplied.Length == expected.Length &&
                    CryptographicOperations.FixedTimeEquals(supplied, expected))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
                // Continue through key-rotation signatures without leaking which value failed.
            }
        }
        return false;
    }

    private static string PadBase64(string value) =>
        (value.Length % 4) switch
        {
            0 => value,
            2 => value + "==",
            3 => value + "=",
            _ => value,
        };
}
