using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DatabaseRecoveryAcceptanceTests
{
    private static readonly JsonSerializerOptions RecoveryEvidenceJson = new() { WriteIndented = true };

    private static async Task<string> ReadSchemaManifestAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT jsonb_build_object(
              'migrations', COALESCE((SELECT jsonb_agg("MigrationId" ORDER BY "MigrationId")
                FROM "__EFMigrationsHistory"), '[]'::jsonb),
              'extensions', COALESCE((SELECT jsonb_agg(jsonb_build_object(
                'name', extname, 'version', extversion) ORDER BY extname)
                FROM pg_extension), '[]'::jsonb),
              'rowSecurity', COALESCE((SELECT jsonb_agg(jsonb_build_object(
                'schema', scope.nspname, 'table', item.relname,
                'enabled', item.relrowsecurity, 'forced', item.relforcerowsecurity)
                ORDER BY scope.nspname, item.relname)
                FROM pg_class item JOIN pg_namespace scope ON scope.oid = item.relnamespace
                WHERE scope.nspname = 'commercial' AND item.relkind IN ('r', 'p')), '[]'::jsonb),
              'policies', COALESCE((SELECT jsonb_agg(jsonb_build_object(
                'schema', schemaname, 'table', tablename, 'name', policyname,
                'roles', roles, 'command', cmd, 'using', qual, 'check', with_check)
                ORDER BY schemaname, tablename, policyname)
                FROM pg_policies WHERE schemaname = 'commercial'), '[]'::jsonb)
            )::text
            """, connection);
        var manifest = (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The recovery schema manifest is unavailable."));
        using var parsed = JsonDocument.Parse(manifest);
        Assert.NotEmpty(parsed.RootElement.GetProperty("migrations").EnumerateArray());
        Assert.NotEmpty(parsed.RootElement.GetProperty("rowSecurity").EnumerateArray());
        Assert.NotEmpty(parsed.RootElement.GetProperty("policies").EnumerateArray());
        foreach (var extension in new[] { "postgis", "vector", "pgcrypto" })
            Assert.Contains(parsed.RootElement.GetProperty("extensions").EnumerateArray(),
                item => item.GetProperty("name").GetString() == extension);
        return manifest;
    }

    private static async Task RetainRecoveryEvidenceAsync(string manifest, TimeSpan elapsed)
    {
        var directory = Environment.GetEnvironmentVariable("ADVERTIFIED_TEST_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        using var parsed = JsonDocument.Parse(manifest);
        var evidence = new
        {
            scope = "ISOLATED_NONPRODUCTION_WITH_SYNTHETIC_DATA",
            schemaManifest = parsed.RootElement.Clone(),
            schemaSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant(),
            protectedTableCount = parsed.RootElement.GetProperty("rowSecurity").EnumerateArray()
                .Count(item => item.GetProperty("enabled").GetBoolean() && item.GetProperty("forced").GetBoolean()),
            backupRestoreAndVerificationMilliseconds = elapsed.TotalMilliseconds,
            timingBasis = "Pre-provisioned isolated test resources; includes backup, restore and verification, not production RTO.",
            rpoStatus = "NOT_MEASURED_NO_CONCURRENT_WRITE_WORKLOAD",
            tenantAccessChecksPassed = true,
            protectedObjectHashVerified = true,
        };
        await File.WriteAllTextAsync(Path.Combine(directory, "recovery-evidence.json"),
            JsonSerializer.Serialize(evidence, RecoveryEvidenceJson));
    }
}
