using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Api.Endpoints;

// Command identity binds every source byte without creating a base64 document
// copy in the request-hashing buffer. This converter is never used for transport.
internal sealed class InventorySourceIdentityConverter : JsonConverter<InventorySourceFile>
{
    public override InventorySourceFile Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, InventorySourceFile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("fileName", value.FileName);
        writer.WriteString("declaredMediaType", value.DeclaredMediaType);
        writer.WriteNumber("length", value.Content.Length);
        writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(value.Content)));
        writer.WriteEndObject();
    }
}
