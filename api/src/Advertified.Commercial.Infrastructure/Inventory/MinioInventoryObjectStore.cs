using Advertified.Commercial.Application.Inventory;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class MinioInventoryObjectStore(
    IMinioClient client,
    IOptions<InventoryProtectionOptions> options) : IInventoryObjectStore
{
    private readonly string bucket = options.Value.Bucket;

    public async Task PutAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var arguments = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(mediaType);
        await client.PutObjectAsync(arguments, cancellationToken);
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
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucket), cancellationToken);
        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        }
    }
}
