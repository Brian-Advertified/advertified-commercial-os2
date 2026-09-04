using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryProjectionVersion
{
    private const string SemanticPlanVersion = "3";

    internal static string Current(
        InventorySemanticOptions semantic) =>
        semantic.Enabled
            ? Planned(semantic)
            : InventoryExtractionOptions.PinnedAdapterVersion;

    internal static string Planned(
        InventorySemanticOptions semantic)
    {
        var material = string.Join("\n", new[]
        {
            SemanticPlanVersion,
            InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            AgentRuntimeOptions.BedrockProvider,
            semantic.ModelId,
            semantic.PromptVersion,
            semantic.MaximumChunkCharacters.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumChunksPerDocument.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumOutputTokensPerChunk.ToString(
                CultureInfo.InvariantCulture),
            semantic.InputTokenReservePerChunk.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImagesPerDocument.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImagesPerChunk.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImageBytes.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImagePayloadBytesPerChunk.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImageDocumentBytes.ToString(
                CultureInfo.InvariantCulture),
            semantic.MaximumImageInputTokens.ToString(
                CultureInfo.InvariantCulture),
        });
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
        return InventoryExtractionOptions.PinnedAdapterVersion +
            ";semantic/" + hash;
    }
}
