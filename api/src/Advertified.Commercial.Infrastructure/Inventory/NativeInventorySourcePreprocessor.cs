using System.Text;
using Advertified.Commercial.Application.Inventory;
using Microsoft.VisualBasic.FileIO;
using Advertified.Commercial.Domain.MasterData;
using UglyToad.PdfPig;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeInventorySourcePreprocessor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static InventoryExtractionResult Process(
        InventoryExtractionRequest request)
    {
        var (elements, images) = request.DocumentClass switch
        {
            MasterDataCodes.DocumentClasses.Pdf => ReadPdf(request.Content),
            MasterDataCodes.DocumentClasses.Csv => ReadCsv(request.Content),
            MasterDataCodes.DocumentClasses.Docx =>
                NativeOfficeSourcePreprocessor.ReadDocx(request.Content),
            MasterDataCodes.DocumentClasses.Xlsx =>
                NativeOfficeSourcePreprocessor.ReadXlsx(request.Content),
            MasterDataCodes.DocumentClasses.Pptx =>
                NativeOfficeSourcePreprocessor.ReadPptx(request.Content),
            MasterDataCodes.DocumentClasses.Png => ReadImage(
                request.Content, "image/png"),
            MasterDataCodes.DocumentClasses.Jpeg => ReadImage(
                request.Content, "image/jpeg"),
            _ => throw new InventoryExtractionUnavailableException(),
        };
        if (elements.Count == 0 && images.Count == 0)
            throw new InventoryExtractionUnavailableException();
        var providerJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            preprocessing = "native-source-v1",
            request.DocumentClass,
            elementCount = elements.Count,
            imageCount = images.Count,
        });
        return InventoryExtractionContract.Create(
            "native-source-preprocessing",
            "1.0.0",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            providerJson,
            [],
            sourceElements: elements,
            sourceImages: images);
    }

    private static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadPdf(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var document = PdfDocument.Open(stream);
        var elements = document.GetPages()
            .Select(page => new InventoryExtractedSourceElement(
                $"pdf:page={page.Number}",
                $"pdf:page={page.Number}",
                "page",
                page.Number,
                1,
                page.Text))
            .Where(item => !string.IsNullOrWhiteSpace(item.RawValue))
            .ToArray();
        return (elements, []);
    }

    private static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadCsv(byte[] content)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            throw new InventoryExtractionUnavailableException();
        }
        try
        {
            using var parser = new TextFieldParser(new StringReader(text))
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false,
            };
            parser.SetDelimiters(",");
            var elements = new List<InventoryExtractedSourceElement>();
            var row = 0;
            while (!parser.EndOfData)
            {
                row++;
                var fields = parser.ReadFields() ?? [];
                elements.AddRange(fields.Select((value, column) =>
                    new InventoryExtractedSourceElement(
                        $"csv:row={row};column={column + 1}",
                        "csv:document", "cell", row, column + 1, value)));
            }
            return (elements, []);
        }
        catch (MalformedLineException)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    private static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadImage(
        byte[] content,
        string mediaType) =>
        ([], [NativeOfficeSourcePreprocessor.Image(
            "image:ordinal=1", mediaType, content)]);
}
