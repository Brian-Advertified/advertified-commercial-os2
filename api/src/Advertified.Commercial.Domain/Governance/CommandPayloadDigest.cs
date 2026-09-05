using System.Security.Cryptography;
using System.Text.Json;

namespace Advertified.Commercial.Domain.Governance;

public static class CommandPayloadDigest
{
    public static Sha256Digest Create<T>(T value, JsonSerializerOptions? options = null)
    {
        var element = JsonSerializer.SerializeToElement(value, options);
        using var algorithm = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, algorithm, CryptoStreamMode.Write);
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        stream.FlushFinalBlock();
        return new Sha256Digest(Convert.ToHexStringLower(algorithm.Hash!));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
