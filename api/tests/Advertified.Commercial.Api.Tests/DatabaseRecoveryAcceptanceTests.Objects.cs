using System.Net;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DatabaseRecoveryAcceptanceTests
{
    private const string SourceBucket = "advertified-recovery-source";
    private const string TargetBucket = "advertified-recovery-target";
    private const string ObjectMediaType = "text/csv";
    private const string SourceVersionMetadata = "recovery-source-version-id";
    private static readonly byte[] ObjectContent = Encoding.UTF8.GetBytes(
        "product_code,name,channel,geography,rate_minor\n" +
        "RECOVERY-001,Recovery Gantry,OOH,Johannesburg,125000\n");
    private static readonly string ObjectHash = Hash(ObjectContent);
    private static string ProtectedObjectKey => $"protected/{TenantId:N}/{ObjectHash}";
    private static string QuarantineObjectKey =>
        $"quarantine/{TenantId:N}/{ImportId:N}/{ObjectHash}";

    private sealed record ObjectBackupEnvelope(
        string ObjectKey,
        string Sha256,
        long Size,
        string MediaType,
        string SourceVersionId,
        byte[] Content);

    private sealed record RestoredObjectReference(
        string ObjectKey,
        string Sha256,
        long Size,
        string MediaType);

    private static Task PrepareObjectStoresAsync(
        DisposableMinio source,
        DisposableMinio target) => Task.WhenAll(
        source.CreateVersionedBucketAsync(SourceBucket),
        target.CreateVersionedBucketAsync(TargetBucket));

    private static async Task<ObjectBackupEnvelope> CreateObjectBackupAsync(
        DisposableMinio source)
    {
        using var store = CreateObjectStore(source, SourceBucket);
        await store.PutAsync(ProtectedObjectKey, ObjectContent, ObjectMediaType, default);
        var initial = await StatAsync(source, SourceBucket, ProtectedObjectKey);
        Assert.False(string.IsNullOrWhiteSpace(initial.VersionId));

        await store.PutAsync(ProtectedObjectKey, ObjectContent, ObjectMediaType, default);
        var retried = await StatAsync(source, SourceBucket, ProtectedObjectKey);
        Assert.Equal(initial.VersionId, retried.VersionId);

        var changed = ObjectContent.ToArray();
        changed[^1] ^= 0x01;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.PutAsync(ProtectedObjectKey, changed, ObjectMediaType, default));

        var adapterRead = await store.ReadAsync(ProtectedObjectKey, default);
        Assert.Equal(ObjectContent, adapterRead);
        var exactVersion = await ReadVersionAsync(
            source, SourceBucket, ProtectedObjectKey, initial.VersionId);
        Assert.Equal(ObjectContent, exactVersion);
        Assert.Equal(ObjectContent.LongLength, initial.Size);
        Assert.Equal(ObjectMediaType, initial.ContentType, ignoreCase: true);
        await AssertAnonymousDeniedAsync(source, SourceBucket, ProtectedObjectKey);
        return new ObjectBackupEnvelope(
            ProtectedObjectKey, ObjectHash, ObjectContent.LongLength,
            ObjectMediaType, initial.VersionId, exactVersion);
    }

    private static async Task<RestoredObjectReference> ReadRestoredObjectReferenceAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT protected_object_key, source_hash, source_size, declared_media_type
            FROM commercial.inventory_imports
            WHERE id = $1 AND tenant_id = $2
            """, connection);
        command.Parameters.AddWithValue(ImportId);
        command.Parameters.AddWithValue(TenantId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new RestoredObjectReference(
            reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3));
    }

    private static async Task AssertInvalidBackupsLeaveTargetEmptyAsync(
        DisposableMinio target,
        ObjectBackupEnvelope backup,
        RestoredObjectReference reference)
    {
        await AssertObjectMissingAsync(target, TargetBucket, reference.ObjectKey);
        var corrupted = backup.Content.ToArray();
        corrupted[0] ^= 0x01;
        var invalidContent = new[] { Array.Empty<byte>(), corrupted };
        foreach (var content in invalidContent)
        {
            var invalid = backup with { Content = content };
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                RestoreObjectAsync(target, invalid, reference));
            await AssertObjectMissingAsync(target, TargetBucket, reference.ObjectKey);
        }
    }

    private static async Task RestoreObjectAsync(
        DisposableMinio target,
        ObjectBackupEnvelope backup,
        RestoredObjectReference reference)
    {
        ValidateBackup(backup, reference);
        using var stream = new MemoryStream(backup.Content, writable: false);
        var headers = new Dictionary<string, string>
        {
            [SourceVersionMetadata] = backup.SourceVersionId,
        };
        var arguments = new PutObjectArgs()
            .WithBucket(TargetBucket)
            .WithObject(backup.ObjectKey)
            .WithStreamData(stream)
            .WithObjectSize(backup.Size)
            .WithContentType(backup.MediaType)
            .WithHeaders(headers);
        await target.Client.PutObjectAsync(arguments);
    }

    private static void ValidateBackup(
        ObjectBackupEnvelope backup,
        RestoredObjectReference reference)
    {
        var valid = backup.ObjectKey == ProtectedObjectKey &&
            backup.ObjectKey == reference.ObjectKey &&
            backup.Sha256 == ObjectHash &&
            backup.Sha256 == reference.Sha256 &&
            backup.Size == ObjectContent.LongLength &&
            backup.Size == reference.Size &&
            backup.Size == backup.Content.LongLength &&
            string.Equals(backup.MediaType, ObjectMediaType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(backup.MediaType, reference.MediaType, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(backup.SourceVersionId) &&
            HashMatches(backup.Content, backup.Sha256);
        if (!valid) throw new InvalidDataException("The object backup envelope is invalid.");
    }

    private static async Task AssertRestoredObjectAsync(
        DisposableMinio target,
        ObjectBackupEnvelope backup,
        RestoredObjectReference reference)
    {
        using var store = CreateObjectStore(target, TargetBucket);
        var restored = await store.ReadAsync(reference.ObjectKey, default);
        var latest = await StatAsync(target, TargetBucket, reference.ObjectKey);
        Assert.Equal(backup.Content, restored);
        Assert.Equal(reference.Sha256, Hash(restored));
        Assert.Equal(reference.Size, latest.Size);
        Assert.Equal(reference.MediaType, latest.ContentType, ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(latest.VersionId));
        Assert.NotEqual(backup.SourceVersionId, latest.VersionId);
        var sourceVersionId = latest.MetaData
            .FirstOrDefault(pair => string.Equals(
                pair.Key, SourceVersionMetadata, StringComparison.OrdinalIgnoreCase))
            .Value;
        Assert.False(string.IsNullOrWhiteSpace(sourceVersionId));
        Assert.Equal(backup.SourceVersionId, sourceVersionId);

        var exact = await StatAsync(
            target, TargetBucket, reference.ObjectKey, latest.VersionId);
        Assert.Equal(latest.ETag, exact.ETag);
        var scan = await new DeterministicInventoryMalwareScanner()
            .ScanAsync(restored, default);
        Assert.True(scan.IsClean);
        Assert.Null(scan.ThreatName);
        await AssertAnonymousDeniedAsync(target, TargetBucket, reference.ObjectKey);
    }

    private static MinioInventoryObjectStore CreateObjectStore(
        DisposableMinio minio,
        string bucket) => new(
        minio.Client,
        Options.Create(new InventoryProtectionOptions { Bucket = bucket }));

    private static Task<ObjectStat> StatAsync(
        DisposableMinio minio,
        string bucket,
        string objectKey,
        string? versionId = null)
    {
        var arguments = new StatObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey);
        if (versionId is not null) arguments.WithVersionId(versionId);
        return minio.Client.StatObjectAsync(arguments);
    }

    private static async Task<byte[]> ReadVersionAsync(
        DisposableMinio minio,
        string bucket,
        string objectKey,
        string versionId)
    {
        using var destination = new MemoryStream();
        var arguments = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithVersionId(versionId)
            .WithCallbackStream(stream => stream.CopyTo(destination));
        await minio.Client.GetObjectAsync(arguments);
        return destination.ToArray();
    }

    private static async Task AssertObjectMissingAsync(
        DisposableMinio minio,
        string bucket,
        string objectKey) =>
        await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
            StatAsync(minio, bucket, objectKey));

    private static async Task AssertAnonymousDeniedAsync(
        DisposableMinio minio,
        string bucket,
        string objectKey)
    {
        using var anonymous = new HttpClient();
        using var response = await anonymous.GetAsync(minio.ObjectUri(bucket, objectKey));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string Hash(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private static bool HashMatches(byte[] content, string expected)
    {
        if (expected.Length != 64) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(content), Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
