using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using System.Text.Json.Nodes;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class DoclingInventoryExtractionAdapter
{
    internal const string EmbeddedImageProjectionVersion =
        "advertified-embedded-image-docling/1.4.0";

    internal static Task<InventoryExtractionResult>
        ReprojectRetainedAsync(
            InventoryExtractionRequest request,
            string providerJson,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = DoclingInventoryProjection.ReadRows(
            request, providerJson);
        var provider = InventoryExtractionContract.Create(
            "docling",
            InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            providerJson,
            rows);
        var extraction = NativeOfficeInventoryProjection.Apply(
            request, provider);
        return Task.FromResult(extraction);
    }

    private async Task<InventoryExtractionResult>
        EnrichEmbeddedOfficeImagesAsync(
            InventoryExtractionRequest request,
            InventoryExtractionResult extraction,
            CancellationToken cancellationToken)
    {
        if (!NativeOfficeImageReader.IsRequired(extraction.Rows))
            return extraction;

        IReadOnlyList<InventoryOfficeImage> images;
        try
        {
            images = NativeOfficeImageReader.ReadContent(
                request, new InventorySemanticOptions());
        }
        catch (InventorySemanticInputRejectedException)
        {
            return extraction;
        }

        var projected = new List<InventoryExtractedRow>();
        var retainedImages = new JsonArray();
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageRequest = ImageRequest(request, image);
            if (imageRequest is null)
            {
                retainedImages.Add(MissingImageEvidence(image));
                continue;
            }
            try
            {
                var result = await ExtractEmbeddedImageAsync(
                    imageRequest, cancellationToken);
                projected.AddRange(result.Rows
                    .Where(IsSellableEmbeddedRow)
                    .Select(row => Rebase(row, image.Locator)));
                retainedImages.Add(new JsonObject
                {
                    ["sourceLocator"] = image.Locator,
                    ["sourceHash"] = image.Sha256,
                    ["document"] = JsonNode.Parse(result.ProviderJson),
                });
            }
            catch (Exception error) when (
                error is InventoryExtractionUnavailableException or
                    InventoryExtractionSubmissionRejectedException)
            {
                // Preserve the original visual-review blocker when local OCR
                // cannot safely reconstruct the embedded image.
                retainedImages.Add(MissingImageEvidence(image));
            }
        }
        if (retainedImages.Count == 0)
            return extraction;

        var rows = extraction.Rows
                    .Where(row =>
                        !NativeOfficeImageReader.IsRequired([row]))
                    .Concat(projected)
            .Select((row, index) =>
                row with { Number = index + 1 })
            .ToArray();
        var provider = JsonNode.Parse(extraction.ProviderJson)!.AsObject();
        provider["embeddedOfficeImages"] = retainedImages;
        return InventoryExtractionContract.Create(
            extraction.AdapterCode,
            InventoryExtractionOptions.PinnedAdapterVersion,
            extraction.SchemaVersion,
            extraction.SourceHash,
            provider.ToJsonString(),
            rows);
    }

    private static JsonObject MissingImageEvidence(InventoryOfficeImage image) => new()
    {
        ["sourceLocator"] = image.Locator,
        ["sourceHash"] = image.Sha256,
        ["document"] = null,
    };

    private async Task<InventoryExtractionResult> ExtractEmbeddedImageAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        // Embedded Office images are already bounded and hash-verified. Use
        // Docling's synchronous endpoint so a stale async queue cannot strand
        // a single small image in PENDING/STARTED indefinitely.
        using var form = CreateForm(request);
        using var result = await ReadJsonAsync(
            HttpMethod.Post,
            "/v1/convert/file",
            form,
            true,
            cancellationToken);
        return MapResult(request, result.RootElement);
    }

    private static InventoryExtractionRequest? ImageRequest(
        InventoryExtractionRequest source,
        InventoryOfficeImage image)
    {
        var metadata = image.Format switch
        {
            "png" => (
                MediaType: "image/png",
                DocumentClass: MasterDataCodes.DocumentClasses.Png,
                Extension: ".png"),
            "jpeg" => (
                MediaType: "image/jpeg",
                DocumentClass: MasterDataCodes.DocumentClasses.Jpeg,
                Extension: ".jpg"),
            _ => default,
        };
        if (metadata.MediaType is null)
            return null;
        var name = "embedded-office-image-" + image.Ordinal +
            metadata.Extension;
        return new InventoryExtractionRequest(
            name,
            metadata.MediaType,
            metadata.DocumentClass,
            image.Sha256,
            image.Content);
    }

    private static bool IsSellableEmbeddedRow(
        InventoryExtractedRow row)
    {
        var values = InventoryCandidateNormalizer.Normalize(
            row, new string('0', 64), DateTimeOffset.UnixEpoch).Values;
        var hasIdentity =
            !string.IsNullOrWhiteSpace(values.ProductCode) ||
            !string.IsNullOrWhiteSpace(values.Name) ||
            values.Package?.PackageCode is not null;
        return hasIdentity &&
            (row.Values.ContainsKey("rate") ||
             values.RateAmountMinor.HasValue ||
             values.Deliverable is not null ||
             values.Package is not null ||
             values.Address is not null ||
             values.Geography is not null ||
             values.Spatial is not null);
    }

    private static InventoryExtractedRow Rebase(
        InventoryExtractedRow row,
        string imageLocator) =>
        row with
        {
            Locator = RebaseLocator(imageLocator, row.Locator),
            FieldLocators = row.FieldLocators?.ToDictionary(
                item => item.Key,
                item => RebaseLocator(imageLocator, item.Value),
                StringComparer.Ordinal),
        };

    private static string RebaseLocator(
        string imageLocator,
        string locator) =>
        imageLocator + ";" + locator.Replace(
            "docling:", "local-ocr:", StringComparison.Ordinal);
}
