using System.Text.Json;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Proposal;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    [Theory]
    [InlineData("OOH", "OOH")]
    [InlineData("OOH", "Outdoor advertising")]
    [InlineData("DOOH", "Digital screens")]
    public async Task ProposalAdapterPreservesExactApprovedOptionFacts(string channel, string wording)
    {
        var narrative = "The approved business problem is enquiries are declining. " +
            "The approved objective is increase enquiries. Success measures: Qualified enquiries; Store visits. " +
            $"Launch invests ZAR 10,000.01 across {wording} to build qualified response. " +
            "Scale invests ZAR 20,000 across DIGITAL to increase consideration.";
        var client = CreateClient(async request =>
        {
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStreamAsync(CancellationToken.None));
            var references = body.RootElement.GetProperty("invocation")
                .GetProperty("resource_refs");
            Assert.Equal(3, references.GetArrayLength());
            Assert.Contains(references.EnumerateArray(), reference =>
                reference.GetProperty("resource_type").GetString() == "MediaPlanVersion" &&
                reference.GetProperty("resource_id").GetGuid() ==
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") &&
                reference.GetProperty("version").GetInt32() == 2);
            return Response(new { executive_summary = narrative }, [EvidenceId]);
        });
        var adapter = new HttpProposalNarrativeClient(
            client, Settings(), ProposalPolicy.Load());

        var result = await adapter.CreateAsync(
            ProposalInput(channel), CancellationToken.None);

        Assert.Equal(narrative, result.ExecutiveSummary);
        Assert.Equal(0, result.IncrementalCostMinor);
    }

    [Theory]
    [InlineData("ZAR 9,000", "OOH")]
    [InlineData("ZAR 10,000.01", "DOOH")]
    [InlineData("ZAR 10,000.01", "Digital screens")]
    public async Task ProposalAdapterRejectsAlteredCommercialValue(string amount, string channel)
    {
        var altered = "The approved business problem is enquiries are declining. " +
            "The approved objective is increase enquiries. Success measures: Qualified enquiries; Store visits. " +
            $"Launch invests {amount} across {channel} to build qualified response. " +
            "Scale invests ZAR 20,000 across DIGITAL to increase consideration.";
        var client = CreateClient(request => Task.FromResult(Response(
            new { executive_summary = altered }, [EvidenceId])));
        var adapter = new HttpProposalNarrativeClient(
            client, Settings(), ProposalPolicy.Load());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.CreateAsync(ProposalInput(), CancellationToken.None));
    }

    [Fact]
    public async Task InProcessProposalAdapterAlsoPreservesMinorUnits()
    {
        var adapter = new ProposalNarrativeFixture(ProposalPolicy.Load());

        var result = await adapter.CreateAsync(ProposalInput(), CancellationToken.None);

        Assert.Contains("ZAR 10,000.01", result.ExecutiveSummary, StringComparison.Ordinal);
    }

    private static ProposalNarrativeInput ProposalInput(string firstChannel = "OOH") => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        BriefVersionId,
        3,
        "Enquiries are declining",
        "Increase enquiries",
        ["Qualified enquiries", "Store visits"],
        [EvidenceId],
        [
            new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 2,
                "Launch", "Build qualified response", 1_000_001, "ZAR", [firstChannel]),
            new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 3,
                "Scale", "Increase consideration", 2_000_000, "ZAR", ["DIGITAL"]),
        ]);
}
