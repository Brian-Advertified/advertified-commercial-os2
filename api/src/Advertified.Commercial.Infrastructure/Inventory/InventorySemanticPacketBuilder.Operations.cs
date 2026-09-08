using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticPacketBuilder
{
    internal static IReadOnlyList<InventorySemanticPacket>
        BuildTranscription(
            InventoryExtractionResult extraction,
            InventoryCodeSets codeSets,
            InventorySemanticOptions settings)
    {
        var items = ReadItems(extraction, settings);
        var images = ReadImages(extraction, settings);
        var sources = BuildSources(items, images, settings).ToArray();
        if (sources.Length == 0 ||
            sources.Length > settings.MaximumChunksPerDocument)
            throw new InventorySemanticInputRejectedException();
        var codes = new InventorySemanticCodes(
            Sorted(codeSets.Channels),
            Sorted(codeSets.ProductTypes),
            Sorted(codeSets.RateTypes),
            Sorted(codeSets.Currencies),
            Sorted(codeSets.Availability));
        return sources.Select((source, index) =>
            CreateTranscriptionPacket(
                codes, source, index + 1, sources.Length, settings))
            .ToArray();
    }

    internal static IReadOnlyList<InventorySemanticPacket>
        BuildEnrichment(
            InventoryExtractionResult extraction,
            InventoryCodeSets codeSets,
            InventorySemanticOptions settings)
    {
        var items = ReadItems(
            extraction,
            settings);
        // Bedrock receives only deterministic rows and their source text.
        var sources = BuildSources(items, [], settings)
            .ToArray();
        var plans = BuildEnrichmentPlans(
            extraction.Rows,
            sources);
        return BuildPackets(
            codeSets,
            settings,
            plans);
    }

    private static InventorySemanticPacketPlan[]
        BuildEnrichmentPlans(
            IReadOnlyList<InventoryExtractedRow> rows,
            InventorySemanticPacketSources[] sources)
    {
        var eligible = rows
            .Where(row =>
                !row.Values.ContainsKey("extractionblocker"))
            .ToArray();
        var duplicateLocator = eligible
            .GroupBy(row => row.Locator, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (duplicateLocator)
        {
            throw new InvalidOperationException(
                "Deterministic inventory row locators must be unique.");
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var plans = new List<InventorySemanticPacketPlan>();
        foreach (var source in sources)
        {
            var related = RelatedRows(
                    rows,
                    source.Items,
                    source.Images)
                .Where(row => !assigned.Contains(row.Locator))
                .ToArray();
            foreach (var group in related.Chunk(200))
            {
                foreach (var row in group)
                    assigned.Add(row.Locator);
                plans.Add(new InventorySemanticPacketPlan(
                    source,
                    group));
            }
        }
        if (assigned.Count != eligible.Length)
        {
            throw new InvalidOperationException(
                "Every deterministic inventory row must be source-grounded " +
                "before semantic enrichment.");
        }
        return plans.ToArray();
    }

    private static InventorySemanticPacket[] BuildPackets(
        InventoryCodeSets codeSets,
        InventorySemanticOptions settings,
        InventorySemanticPacketPlan[] plans)
    {
        if (plans.Length > settings.MaximumChunksPerDocument)
        {
            throw new InvalidOperationException(
                "The inventory enrichment plan exceeds its bounds.");
        }
        var codes = new InventorySemanticCodes(
            Sorted(codeSets.Channels),
            Sorted(codeSets.ProductTypes),
            Sorted(codeSets.RateTypes),
            Sorted(codeSets.Currencies),
            Sorted(codeSets.Availability));
        return plans.Select((plan, index) =>
            CreatePacket(
                codes,
                plan,
                index + 1,
                plans.Length,
                settings)).ToArray();
    }

    private static InventorySemanticPacket CreatePacket(
        InventorySemanticCodes codes,
        InventorySemanticPacketPlan plan,
        int number,
        int count,
        InventorySemanticOptions settings)
    {
        var planJson = JsonSerializer.Serialize(new
        {
            promptVersion = settings.PromptVersion,
            operation = InventorySemanticOperations.SemanticEnrichment,
            chunkNumber = number,
            chunkCount = count,
            sourceItems = plan.Sources.Items,
            sourceImages = Array.Empty<object>(),
            existingRows = plan.ExistingRows,
            governedCodes = codes,
        }, WireJson);
        var inputHash = Hash(planJson);
        return new InventorySemanticPacket(
            StepId(inputHash),
            InventorySemanticOperations.SemanticEnrichment,
            number,
            count,
            inputHash,
            planJson,
            plan.Sources.Items,
            plan.ExistingRows,
            [],
            settings.MaximumCostUsdMicros(
                planJson.Length,
                0));
    }

    private static InventorySemanticPacket CreateTranscriptionPacket(
        InventorySemanticCodes codes,
        InventorySemanticPacketSources source,
        int number,
        int count,
        InventorySemanticOptions settings)
    {
        var planJson = JsonSerializer.Serialize(new
        {
            promptVersion = settings.PromptVersion,
            operation = InventorySemanticOperations.SourceTranscription,
            chunkNumber = number,
            chunkCount = count,
            sourceItems = source.Items,
            sourceImages = source.Images,
            existingRows = Array.Empty<object>(),
            governedCodes = codes,
        }, WireJson);
        var inputHash = Hash(planJson);
        return new InventorySemanticPacket(
            StepId(inputHash),
            InventorySemanticOperations.SourceTranscription,
            number,
            count,
            inputHash,
            planJson,
            source.Items,
            [],
            source.Images,
            settings.MaximumCostUsdMicros(
                planJson.Length,
                source.Images.Count));
    }

    private static InventorySemanticImage[] ReadImages(
        InventoryExtractionResult extraction,
        InventorySemanticOptions settings)
    {
        var source = extraction.Document.SourceImages ?? [];
        if (source.Count > settings.MaximumImagesPerDocument ||
            source.Sum(image => (long)image.ByteLength) >
                settings.MaximumImageDocumentBytes ||
            source.Any(image =>
                image.ByteLength <= 0 ||
                image.ByteLength > settings.MaximumImageBytes))
            throw new InventorySemanticInputRejectedException();
        return source.Select((image, index) => new InventorySemanticImage(
            index + 1,
            image.Locator,
            image.MediaType,
            image.Base64Content,
            image.ByteLength,
            image.Sha256)).ToArray();
    }
}

internal sealed record InventorySemanticPacketPlan(
    InventorySemanticPacketSources Sources,
    IReadOnlyList<InventorySemanticExistingRow> ExistingRows);
