namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySemanticOptions
{
    public const string SectionName = "InventorySemantic";
    public const long MaximumCertificationBudgetUsdMicros =
        5_000_000;

    public bool Enabled { get; init; }
    public int MaximumChunkCharacters { get; init; } = 18_000;
    public int MaximumChunksPerDocument { get; init; } = 256;
    public int MaximumOutputTokensPerChunk { get; init; } = 2_048;
    public int InputTokenReservePerChunk { get; init; } = 32_768;
    public int MaximumImagesPerDocument { get; init; } = 256;
    public int MaximumImagesPerChunk { get; init; } = 20;
    public int MaximumImageBytes { get; init; } = 3_750_000;
    public int MaximumImagePayloadBytesPerChunk { get; init; } =
        20_000_000;
    public int MaximumImageDocumentBytes { get; init; } = 64_000_000;
    public int MaximumImageInputTokens { get; init; } = 4_096;
    public string ModelId { get; init; } = string.Empty;
    public long InputPricePerMillionTokensUsdMicros { get; init; }
    public long OutputPricePerMillionTokensUsdMicros { get; init; }
    public long PerCallCostCapUsdMicros { get; init; }
    public long CertificationBudgetUsdMicros { get; init; } =
        MaximumCertificationBudgetUsdMicros;
    public string BudgetScope { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = "4.0.0";

    public static bool IsValid(InventorySemanticOptions value) =>
        !value.Enabled || IsPlanningValid(value);

    internal static bool IsPlanningValid(
        InventorySemanticOptions value) =>
        HasValidChunkLimits(value) &&
        HasValidImageLimits(value) &&
        HasValidProviderBudget(value) &&
        HasValidIdentity(value);

    private static bool HasValidChunkLimits(
        InventorySemanticOptions value) =>
        value.MaximumChunkCharacters is >= 2_000 and <= 24_000 &&
        value.MaximumChunksPerDocument is >= 1 and <= 256 &&
        value.MaximumOutputTokensPerChunk is >= 128 and <= 8_192 &&
        value.InputTokenReservePerChunk is >= 1_024 and <= 131_072;

    private static bool HasValidImageLimits(
        InventorySemanticOptions value) =>
        value.MaximumImagesPerDocument is >= 1 and <= 256 &&
        value.MaximumImagesPerChunk is >= 1 and <= 20 &&
        value.MaximumImagesPerChunk <=
            value.MaximumImagesPerDocument &&
        value.MaximumImageBytes is >= 1_024 and <= 3_750_000 &&
        value.MaximumImagePayloadBytesPerChunk >=
            value.MaximumImageBytes &&
        value.MaximumImagePayloadBytesPerChunk <= 20_000_000 &&
        value.MaximumImageDocumentBytes >=
            value.MaximumImagePayloadBytesPerChunk &&
        value.MaximumImageDocumentBytes <= 100_000_000 &&
        value.MaximumImageInputTokens is >= 230 and <= 32_768;

    private static bool HasValidProviderBudget(
        InventorySemanticOptions value) =>
        !string.IsNullOrWhiteSpace(value.ModelId) &&
        value.ModelId != "fixture-v1" &&
        value.ModelId.Length <= 300 &&
        value.ModelId.All(character => !char.IsWhiteSpace(character)) &&
        value.InputPricePerMillionTokensUsdMicros > 0 &&
        value.OutputPricePerMillionTokensUsdMicros > 0 &&
        value.PerCallCostCapUsdMicros > 0 &&
        value.CertificationBudgetUsdMicros is > 0 and
            <= MaximumCertificationBudgetUsdMicros &&
        value.PerCallCostCapUsdMicros <=
            value.CertificationBudgetUsdMicros;

    private static bool HasValidIdentity(
        InventorySemanticOptions value) =>
        !string.IsNullOrWhiteSpace(value.BudgetScope) &&
        value.BudgetScope.Length <= 200 &&
        value.BudgetScope.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.') &&
        System.Text.RegularExpressions.Regex.IsMatch(
            value.PromptVersion,
            @"^[1-9][0-9]*\.[0-9]+\.[0-9]+$");

    internal long MaximumCostUsdMicros(
        int inputCharacters,
        int imageCount)
    {
        if (imageCount < 0 || imageCount > MaximumImagesPerChunk)
            throw new InventorySemanticInputRejectedException();
        var inputTokens = (inputCharacters + 2L) / 3L +
            InputTokenReservePerChunk +
            (long)imageCount * MaximumImageInputTokens;
        var numerator =
            inputTokens * InputPricePerMillionTokensUsdMicros +
            (long)MaximumOutputTokensPerChunk *
                OutputPricePerMillionTokensUsdMicros;
        return (numerator + 999_999L) / 1_000_000L;
    }
}

internal sealed class InventorySemanticInputRejectedException :
    Exception
{
}
