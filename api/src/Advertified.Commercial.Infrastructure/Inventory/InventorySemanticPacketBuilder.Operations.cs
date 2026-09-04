using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticPacketBuilder
{
    internal static IReadOnlyList<InventorySemanticPacket> Build(
        InventoryExtractionRequest request,
        InventoryExtractionResult extraction,
        InventoryCodeSets codeSets,
        InventorySemanticOptions settings) =>
        BuildEnrichment(request, extraction, codeSets, settings);

    internal static IReadOnlyList<InventorySemanticPacket>
        BuildEnrichment(
            InventoryExtractionRequest request,
            InventoryExtractionResult extraction,
            InventoryCodeSets codeSets,
            InventorySemanticOptions settings)
    {
        var items = ReadItems(
            request,
            extraction.ProviderJson,
            extraction.Rows,
            settings);
        // Embedded images are handled by local Docling OCR before this stage.
        // Bedrock receives only deterministic rows and their source text.
        var sources = BuildSources(items, [], settings)
            .ToArray();
        var plans = BuildEnrichmentPlans(
            extraction.Rows,
            sources);
        return BuildPackets(
            request,
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
        InventoryExtractionRequest request,
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
                request,
                codes,
                plan,
                index + 1,
                plans.Length,
                settings)).ToArray();
    }

    private static InventorySemanticPacket CreatePacket(
        InventoryExtractionRequest request,
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
            request.SourceHash,
            request.FileName,
            request.DocumentClass,
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
}

internal sealed record InventorySemanticPacketPlan(
    InventorySemanticPacketSources Sources,
    IReadOnlyList<InventorySemanticExistingRow> ExistingRows);
