using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class ReferenceObservationReader
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static async Task<IReadOnlyList<ReferenceObservationFact>> ReadAsync(
        GovernanceDbContext database,
        IReadOnlyList<string> geographies,
        string domainCode,
        IReadOnlyList<string> activationPolicies,
        int maximum,
        CancellationToken cancellationToken)
    {
        if (maximum is < 1 or > 2_000)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (string.IsNullOrWhiteSpace(domainCode) || activationPolicies.Count == 0)
            throw new ArgumentException("Reference observation scope is required.");

        var geographyValues = geographies.ToArray();
        var policyValues = activationPolicies.ToArray();
        var national = geographyValues.Any(item =>
            string.Equals(item, "South Africa", StringComparison.OrdinalIgnoreCase));
        var rows = await database.Database.SqlQuery<ReferenceObservationRow>($"""
            SELECT observation.id AS "ObservationId", source.title AS "SourceTitle",
                source.measurement_period AS "MeasurementPeriod",
                observation.geography_level AS "GeographyLevel",
                observation.geography_code AS "GeographyCode",
                observation.geography_name AS "GeographyName",
                observation.dimensions_json::text AS "DimensionsJson",
                observation.metric_code AS "MetricCode",
                observation.metric_value AS "MetricValue",
                observation.metric_unit AS "MetricUnit",
                observation.stability_code AS "StabilityCode",
                observation.sensitivity_code AS "SensitivityCode",
                observation.activation_policy AS "ActivationPolicy",
                observation.evidence_notes_json::text AS "EvidenceNotesJson"
            FROM governance.intelligence_observations observation
            JOIN governance.intelligence_sources source ON source.id = observation.source_id
            WHERE source.is_current
              AND observation.domain_code = {domainCode}
              AND observation.activation_policy = ANY({policyValues})
              AND ({national} OR observation.geography_level = 'COUNTRY'
                   OR observation.geography_name = ANY({geographyValues}))
            ORDER BY observation.sensitivity_code, source.measurement_period DESC,
                observation.geography_level, observation.geography_name,
                observation.metric_code, observation.id
            LIMIT {maximum}
            """).ToArrayAsync(cancellationToken);

        return rows.Select(row => new ReferenceObservationFact(
            row.ObservationId,
            row.SourceTitle,
            row.MeasurementPeriod,
            row.GeographyLevel ?? "UNSPECIFIED",
            row.GeographyCode ?? "UNSPECIFIED",
            row.GeographyName ?? "Unspecified",
            JsonSerializer.Deserialize<Dictionary<string, string>>(row.DimensionsJson, StoredJson)
                ?? new Dictionary<string, string>(),
            row.MetricCode,
            row.MetricValue,
            row.MetricUnit,
            row.StabilityCode,
            row.SensitivityCode,
            row.ActivationPolicy,
            JsonSerializer.Deserialize<string[]>(row.EvidenceNotesJson, StoredJson) ?? [])).ToArray();
    }

    private sealed record ReferenceObservationRow(
        Guid ObservationId,
        string SourceTitle,
        string MeasurementPeriod,
        string? GeographyLevel,
        string? GeographyCode,
        string? GeographyName,
        string DimensionsJson,
        string MetricCode,
        decimal MetricValue,
        string MetricUnit,
        string StabilityCode,
        string SensitivityCode,
        string ActivationPolicy,
        string EvidenceNotesJson);
}
