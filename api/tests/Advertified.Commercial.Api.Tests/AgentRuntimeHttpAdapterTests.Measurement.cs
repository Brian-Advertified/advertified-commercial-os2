using System.Text.Json;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Infrastructure.Measurement;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    private static readonly Guid MeasurementMetricId = Guid.Parse(
        "12121212-1212-1212-1212-121212121212");

    [Fact]
    public async Task MeasurementAdapterUsesExactCampaignEvidenceAndZeroCostBoundary()
    {
        var client = CreateClient(async request =>
        {
            Assert.Equal("/v1/agents/measurement", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStreamAsync(CancellationToken.None));
            var invocation = body.RootElement.GetProperty("invocation");
            Assert.Equal("measurement", invocation.GetProperty("agent_code").GetString());
            Assert.Equal(MeasurementMetricId,
                invocation.GetProperty("approved_evidence_item_ids")[0].GetGuid());
            Assert.Equal("Campaign", invocation.GetProperty("resource_refs")[0]
                .GetProperty("resource_type").GetString());
            return Response(MeasurementArtifact(), [MeasurementMetricId]);
        });
        var adapter = new HttpMeasurementAgentClient(client, Settings());

        var proposal = await adapter.InterpretAsync(
            MeasurementInput(), CancellationToken.None);

        Assert.Equal("NOT_ESTABLISHED", proposal.Interpretation.CausalityStatus);
        Assert.Equal(0, proposal.IncrementalCostMinor);
        Assert.Equal(MeasurementMetricId,
            Assert.Single(proposal.Interpretation.Findings).MetricIds.Single());
    }

    [Theory]
    [InlineData("wrong-metric")]
    [InlineData("dropped-limitation")]
    [InlineData("unsupported-causality")]
    [InlineData("non-zero-cost")]
    public async Task MeasurementAdapterRejectsUnsafeOrUnboundOutput(string scenario)
    {
        var metricIds = scenario == "wrong-metric"
            ? new[] { Guid.Parse("13131313-1313-1313-1313-131313131313") }
            : new[] { MeasurementMetricId };
        var limitations = scenario == "dropped-limitation"
            ? Array.Empty<string>()
            : new[] { "Panel data excludes devices without consent." };
        var causality = scenario == "unsupported-causality"
            ? "ESTABLISHED"
            : "NOT_ESTABLISHED";
        var artifact = MeasurementArtifact(metricIds, limitations, causality);
        var client = CreateClient(request => Task.FromResult(Response(
            artifact, [MeasurementMetricId], scenario == "non-zero-cost" ? 1 : 0)));
        var adapter = new HttpMeasurementAgentClient(client, Settings());

        await Assert.ThrowsAsync<MeasurementAgentOutputRejectedException>(() =>
            adapter.InterpretAsync(MeasurementInput(), CancellationToken.None));
    }

    [Fact]
    public async Task MeasurementAdapterRejectsUnknownResponseFields()
    {
        var response = Response(MeasurementArtifact(), [MeasurementMetricId]);
        response.Content = JsonString(
            (await response.Content.ReadAsStringAsync(CancellationToken.None))
                .Replace("\"usage\":", "\"unexpected\":true,\"usage\":",
                    StringComparison.Ordinal));
        var adapter = new HttpMeasurementAgentClient(
            CreateClient(request => Task.FromResult(response)), Settings());

        await Assert.ThrowsAsync<MeasurementAgentOutputRejectedException>(() =>
            adapter.InterpretAsync(MeasurementInput(), CancellationToken.None));
    }

    private static MeasurementAgentInput MeasurementInput()
    {
        var evidenceId = Guid.Parse("14141414-1414-1414-1414-141414141414");
        return new MeasurementAgentInput(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("16161616-1616-1616-1616-161616161616"), 4,
            ["Track sourced impressions for the booked flight."],
            [new MeasurementProofInput(
                Guid.Parse("17171717-1717-1717-1717-171717171717"), 2)],
            [new MeasurementEvidenceInput(
                evidenceId, 2, "VERIFIED", "Verified supplier delivery logs.",
                ["Panel data excludes devices without consent."],
                [new MeasurementMetricFactInput(
                    MeasurementMetricId, evidenceId, "IMPRESSIONS", 125_000m,
                    "COUNT", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
                    "verified.json#/facts/impressions")])]);
    }

    private static object MeasurementArtifact(
        IReadOnlyList<Guid>? metricIds = null,
        IReadOnlyList<string>? limitations = null,
        string causality = "NOT_ESTABLISHED") => new
        {
            executive_summary = "Reviewed facts retain their source limitations.",
            findings = new[]
            {
                new
                {
                    title = "Impressions reported",
                    summary = "The source reports an observed fact without attribution.",
                    metric_ids = metricIds ?? [MeasurementMetricId],
                    causality_status = causality,
                },
            },
            limitations = limitations ?? ["Panel data excludes devices without consent."],
            learning_proposals = new[]
            {
                new { text = "Use as a learning input after approval.",
                    requires_new_approval = true },
            },
            causality_status = causality,
        };
}
