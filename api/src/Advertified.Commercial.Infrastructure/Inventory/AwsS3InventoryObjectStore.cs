using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Advertified.Commercial.Application.Inventory;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class AwsS3InventoryObjectStore(
    IAmazonS3 client,
    IOptions<InventoryProtectionOptions> options) : IInventoryObjectStore
{
    private readonly string bucket = options.Value.Bucket;

    public async Task PutAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var existing = await TryMetadataAsync(objectKey, cancellationToken);
        if (existing is not null)
        {
            await EnsureSameObjectAsync(
                objectKey, content, mediaType, existing, cancellationToken);
            return;
        }

        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = mediaType,
            AutoCloseStream = false,
            IfNoneMatch = "*",
        };
        try
        {
            await client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            var raced = await TryMetadataAsync(objectKey, cancellationToken)
                ?? throw new InventoryProtectionUnavailableException();
            await EnsureSameObjectAsync(
                objectKey, content, mediaType, raced, cancellationToken);
            return;
        }
        await VerifyWriteAsync(objectKey, content, mediaType, cancellationToken);
    }

    public async Task<byte[]> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetObjectAsync(
                new GetObjectRequest { BucketName = bucket, Key = objectKey },
                cancellationToken);
            using var destination = new MemoryStream();
            await response.ResponseStream.CopyToAsync(destination, cancellationToken);
            return destination.ToArray();
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The protected object does not exist.", objectKey);
        }
    }

    public async Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken) =>
        await TryMetadataAsync(objectKey, cancellationToken) is not null;

    private async Task<GetObjectMetadataResponse?> TryMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = bucket, Key = objectKey },
                cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureSameObjectAsync(
        string objectKey,
        ReadOnlyMemory<byte> expected,
        string mediaType,
        GetObjectMetadataResponse metadata,
        CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(objectKey, cancellationToken);
        if (metadata.ContentLength != expected.Length ||
            !string.Equals(metadata.Headers.ContentType, mediaType, StringComparison.OrdinalIgnoreCase) ||
            !stored.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InvalidOperationException("An immutable object key was reused.");
        }
    }

    private async Task VerifyWriteAsync(
        string objectKey,
        ReadOnlyMemory<byte> expected,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var metadata = await TryMetadataAsync(objectKey, cancellationToken);
        if (metadata is null)
        {
            throw new InventoryProtectionUnavailableException();
        }
        try
        {
            await EnsureSameObjectAsync(
                objectKey, expected, mediaType, metadata, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new InventoryProtectionUnavailableException();
        }
    }
}
