using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Endpoints;

internal static class InventoryUploadBody
{
    // Bounded transport overhead for the one-file multipart command protocol.
    private const int MetadataAllowanceBytes = 64 * 1024;

    internal static async Task<(IFormCollection Form, InventorySourceFile Source)> ReadAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        var maximum = context.RequestServices
            .GetRequiredService<IOptions<InventoryProtectionOptions>>().Value.MaximumSourceBytes;
        var request = context.Request;
        if (request.ContentLength > maximum + MetadataAllowanceBytes) throw TooLarge();
        var originalBody = request.Body;
        await using var bounded = new FileBufferingReadStream(
            originalBody, MetadataAllowanceBytes, maximum + MetadataAllowanceBytes, Path.GetTempPath);
        request.Body = bounded;
        try
        {
            context.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
            {
                MultipartBodyLengthLimit = maximum,
                MemoryBufferThreshold = MetadataAllowanceBytes,
                ValueCountLimit = 8,
                ValueLengthLimit = MetadataAllowanceBytes,
            }));
            var form = await request.ReadFormAsync(cancellationToken);
            if (form.Files.Count != 1 || form.Files.GetFile("source") is not { } file)
                throw new BadHttpRequestException("Supply exactly one inventory source file.");
            if (file.Length <= 0 || file.Length > maximum) throw TooLarge();
            var content = new byte[checked((int)file.Length)];
            await using var stream = file.OpenReadStream();
            await stream.ReadExactlyAsync(content, cancellationToken);
            return (form, new InventorySourceFile(file.FileName, file.ContentType, content));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            throw TooLarge();
        }
        finally
        {
            request.Body = originalBody;
        }
    }

    private static BadHttpRequestException TooLarge() => new(
        "The inventory upload exceeds its source limit.", StatusCodes.Status413PayloadTooLarge);
}
