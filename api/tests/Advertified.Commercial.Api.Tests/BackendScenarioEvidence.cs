using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

// Test-only observations and receipts, never commercial persistence or a simulator.
internal sealed record BackendScenarioObservation(
    object SourceInputs, object ActualResult, string TerminalState,
    IReadOnlyDictionary<string, bool> Invariants, IReadOnlyList<string> HumanReviewPoints,
    int? UnsupportedFacts, int? MissingRequiredFacts,
    string CommercialReconciliation, string TenantSecurity);

internal static class BackendScenarioEvidence
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true,
    };

    internal static JsonElement Definition(string id) =>
        Definitions().Single(item => item.GetProperty("scenario_id").GetString() == id);

    internal static IEnumerable<object[]> Cases(string category) => Definitions()
        .Where(item => item.GetProperty("category").GetString() == category)
        .Select(item => new object[] { item.GetProperty("scenario_id").GetString()! });

    internal static void Run(string id, Func<BackendScenarioObservation> execute)
    {
        var clock = Stopwatch.StartNew();
        var definition = Definition(id);
        BackendScenarioObservation observed;
        try
        {
            observed = execute();
        }
        catch (Exception error)
        {
            Write(definition, null, clock.ElapsedMilliseconds, error.Message);
            throw;
        }
        var expected = definition.GetProperty("expected_invariants").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()!).ToHashSet(StringComparer.Ordinal);
        var passed = expected.SetEquals(observed.Invariants.Keys) &&
            observed.Invariants.Values.All(value => value);
        Write(definition, observed, clock.ElapsedMilliseconds,
            passed ? null : "Missing or failed observed invariant.");
        Assert.True(passed, JsonSerializer.Serialize(observed.Invariants, Json));
    }

    internal static async Task RunAsync(string id, Func<Task<BackendScenarioObservation>> execute)
    {
        var clock = Stopwatch.StartNew();
        var definition = Definition(id);
        BackendScenarioObservation observed;
        try
        {
            observed = await execute();
        }
        catch (Exception error)
        {
            Write(definition, null, clock.ElapsedMilliseconds, error.Message);
            throw;
        }
        var expected = definition.GetProperty("expected_invariants").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()!).ToHashSet(StringComparer.Ordinal);
        var passed = expected.SetEquals(observed.Invariants.Keys) && observed.Invariants.Values.All(value => value);
        Write(definition, observed, clock.ElapsedMilliseconds, passed ? null : "Missing or failed observed invariant.");
        Assert.True(passed, JsonSerializer.Serialize(observed.Invariants, Json));
    }

    private static void Write(
        JsonElement definition, BackendScenarioObservation? observed, long milliseconds, string? error)
    {
        var id = definition.GetProperty("scenario_id").GetString()!;
        var directory = Environment.GetEnvironmentVariable("ADVERTIFIED_TEST_EVIDENCE_DIRECTORY")
            ?? Path.Combine(Path.GetTempPath(), "advertified-backend-scenarios");
        directory = Path.Combine(directory, "scenarios");
        Directory.CreateDirectory(directory);
        var invariantNames = definition.GetProperty("expected_invariants").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()!).ToArray();
        var record = new
        {
            scenario_id = id, category = definition.GetProperty("category").GetString(),
            source_inputs = new { definition = definition.GetProperty("source_inputs"), observed = observed?.SourceInputs },
            invariant_results = observed?.Invariants ?? invariantNames.ToDictionary(name => name, _ => false),
            actual_result = observed?.ActualResult ?? new { error },
            lifecycle_terminal_state = observed?.TerminalState ?? "NOT_OBSERVED",
            human_review_points = observed?.HumanReviewPoints ?? [],
            unsupported_fact_count = observed?.UnsupportedFacts, missing_required_fact_count = observed?.MissingRequiredFacts,
            provider = "deterministic", model = "fixture-v1", tool_calls = 0,
            input_tokens = 0, output_tokens = 0, incremental_cost_minor = 0, cost_currency = "USD",
            latency_ms = milliseconds, retries = 0,
            commercial_reconciliation = observed?.CommercialReconciliation ?? "NOT_VERIFIED",
            tenant_security_result = observed?.TenantSecurity ?? "NOT_VERIFIED",
            passed = error is null, execution_status = error is null ? "EXECUTED" : "EXECUTION_FAILED",
            failure_reasons = error is null ? Array.Empty<string>() : [error],
        };
        File.WriteAllText(Path.Combine(directory, id + ".json"), JsonSerializer.Serialize(record, Json));
    }

    private static JsonElement[] Definitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BackendScenarios", "catalogue.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("scenarios").EnumerateArray().Select(item => item.Clone()).ToArray();
    }
}
