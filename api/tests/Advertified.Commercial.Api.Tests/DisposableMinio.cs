using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Minio;
using Minio.DataModel.Args;

namespace Advertified.Commercial.Api.Tests;

internal sealed class DisposableMinio : IAsyncDisposable
{
    private const ushort ApiPort = 9000;
    private const string AccessKey = "advertified-recovery-local";
    private const string SecretKey = "advertified-recovery-local-only";
    private const string Image =
        "minio/minio:RELEASE.2025-09-07T16-13-09Z@" +
        "sha256:14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e";
    private readonly IContainer container;
    private IMinioClient? client;

    private DisposableMinio(IContainer container) => this.container = container;

    internal IMinioClient Client => client ??= new MinioClient()
        .WithEndpoint(container.Hostname, container.GetMappedPublicPort(ApiPort))
        .WithCredentials(AccessKey, SecretKey)
        .Build();

    internal static DisposableMinio Create() => new(new ContainerBuilder(Image)
        .WithEnvironment("MINIO_ROOT_USER", AccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
        .WithPortBinding(ApiPort, true)
        .WithCommand("server", "/data", "--console-address", ":9001")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(ApiPort)
                .ForPath("/minio/health/ready")
                .ForStatusCode(HttpStatusCode.OK)))
        .Build());

    internal Task StartAsync(CancellationToken cancellationToken = default) =>
        container.StartAsync(cancellationToken);

    internal Task StopAsync(CancellationToken cancellationToken = default) =>
        container.StopAsync(cancellationToken);

    internal Uri ObjectUri(string bucket, string objectKey) => new(
        $"http://{container.Hostname}:{container.GetMappedPublicPort(ApiPort)}/{bucket}/{objectKey}");

    internal async Task CreateVersionedBucketAsync(
        string bucket,
        CancellationToken cancellationToken = default)
    {
        await Client.MakeBucketAsync(
            new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        await Client.SetVersioningAsync(
            new SetVersioningArgs().WithBucket(bucket).WithVersioningEnabled(),
            cancellationToken);
        var versioning = await Client.GetVersioningAsync(
            new GetVersioningArgs().WithBucket(bucket), cancellationToken);
        if (!string.Equals(versioning.Status, "Enabled", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The disposable object bucket is not versioned.");
        }
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
