using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class DoclingInventoryExtractionAdapter
{
    internal const string EmbeddedImageProjectionVersion =
        "advertified-embedded-image-docling/1.2.0";

    internal async Task<InventoryExtractionResult>
        ReprojectRetainedAsync(
            InventoryExtractionRequest request,
            string providerJson,
            CancellationToken cancellationToken)
    {
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
        var enriched = await EnrichEmbeddedOfficeImagesAsync(
            request, extraction, cancellationToken);
        return InventorySourceContextProjection.Apply(
            request, enriched);
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
        var positioning = new List<EmbeddedPositioningContext>();
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageRequest = ImageRequest(request, image);
            if (imageRequest is null)
                continue;
            try
            {
                var result = await ExtractEmbeddedImageAsync(
                    imageRequest, cancellationToken);
                projected.AddRange(result.Rows
                    .Where(IsSellableEmbeddedRow)
                    .Select(row => Rebase(row, image.Locator)));
                var context = ReadPositioningContext(
                    result.ProviderJson,
                    image.Locator);
                if (context is not null)
                    positioning.Add(context);
            }
            catch (Exception error) when (
                error is InventoryExtractionUnavailableException or
                    InventoryExtractionSubmissionRejectedException)
            {
                // Preserve the original visual-review blocker when local OCR
                // cannot safely reconstruct the embedded image.
            }
        }
        if (projected.Count == 0)
            return extraction;

        var rows = ApplyPositioningContext(
                extraction.Rows
                    .Where(row =>
                        !NativeOfficeImageReader.IsRequired([row]))
                    .Concat(projected)
                    .ToArray(),
                positioning)
            .Select((row, index) =>
                row with { Number = index + 1 })
            .ToArray();
        return InventoryExtractionContract.Create(
            extraction.AdapterCode,
            InventoryExtractionOptions.PinnedAdapterVersion,
            extraction.SchemaVersion,
            extraction.SourceHash,
            extraction.ProviderJson,
            rows);
    }

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
