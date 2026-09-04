using Npgsql;

namespace Advertified.Commercial.Api.Tests;

internal static class InventoryProjectionSeed
{
    internal static Guid Add(
        NpgsqlBatch batch,
        Guid tenantId,
        Guid importId,
        Guid createdBy,
        DateTimeOffset createdAtUtc,
        char hashCharacter)
    {
        var projectionId = Guid.NewGuid();
        var hash = new string(hashCharacter, 64);
        AddCommand(batch, """
            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash,
                adapter_code, adapter_version, schema_version,
                provider_json, provider_output_hash,
                completed_at_utc)
            VALUES ($1, $2, $3, $4, 'test-seed', '1.0.0',
                'advertified.inventory-extraction.v3', '{}', $4, $5)
            """, projectionId, tenantId, importId, hash, createdAtUtc);
        AddCommand(batch, """
            INSERT INTO commercial.inventory_extraction_projections (
                id, tenant_id, import_id, input_artifact_id,
                projector_code, projector_version, schema_version,
                canonical_output_hash, candidate_count,
                created_by, created_at_utc)
            VALUES ($1, $2, $3, $1, 'test-seed', '1.0.0',
                'advertified.inventory-extraction.v3', $4, 1, $5, $6)
            """, projectionId, tenantId, importId, hash,
            createdBy, createdAtUtc);
        return projectionId;
    }

    private static void AddCommand(
        NpgsqlBatch batch,
        string sql,
        params object[] values)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in values)
            command.Parameters.AddWithValue(value);
        batch.BatchCommands.Add(command);
    }
}
