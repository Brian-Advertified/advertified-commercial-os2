using Advertified.Commercial.Application.Inventory;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class MinioInventoryObjectStore(
    IMinioClient client,
    IOptions<InventoryProtectionOptions> options) : IInventoryObjectStore, IDisposable
{
    private readonly string bucket = options.Value.Bucket;
    private readonly SemaphoreSlim bucketGate = new(1, 1);
    private bool bucketReady;

    public async Task PutAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        var existing = await TryStatAsync(objectKey, cancellationToken);
        if (existing is not null)
        {
            var current = await ReadAsync(objectKey, cancellationToken);
            if (current.AsSpan().SequenceEqual(content.Span) &&
                string.Equals(existing.ContentType, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            throw new InvalidOperationException("An immutable object key was reused.");
        }

        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var arguments = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(mediaType);
        await client.PutObjectAsync(arguments, cancellationToken);
        await VerifyWriteAsync(objectKey, content, mediaType, cancellationToken);
    }

    public async Task<byte[]> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        var arguments = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(destination));
        await client.GetObjectAsync(arguments, cancellationToken);
        return destination.ToArray();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (bucketReady) return;
        await bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (bucketReady) return;
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket), cancellationToken);
            if (!exists)
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucket), cancellationToken);
            }
            bucketReady = true;
        }
        finally { bucketGate.Release(); }
    }

    private async Task<ObjectStat?> TryStatAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.StatObjectAsync(
                new StatObjectArgs().WithBucket(bucket).WithObject(objectKey),
                cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    private async Task VerifyWriteAsync(
        string objectKey,
        ReadOnlyMemory<byte> expected,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var stat = await TryStatAsync(objectKey, cancellationToken);
        var stored = await ReadAsync(objectKey, cancellationToken);
        if (stat is null || stat.Size != expected.Length ||
            !string.Equals(stat.ContentType, mediaType, StringComparison.OrdinalIgnoreCase) ||
            !stored.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InventoryProtectionUnavailableException();
        }
    }

    public void Dispose() => bucketGate.Dispose();
}
