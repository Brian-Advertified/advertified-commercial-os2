using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Infrastructure.Brief;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class SuppliedBriefTimingInterpretationTests
{
    [Fact]
    public async Task SupplierResponseDeadlineIsNotUsedAsCampaignTiming()
    {
        const string content = """
            Deadline: please share availability by 16 July 2026 COB
            Campaign dates: 1 October 2026 to 31 December 2026
            Media: OOH billboards
            """;
        var client = new DeterministicSuppliedBriefAgentClient(
            SuppliedBriefAgentPolicy.Load());

        var result = await client.UnderstandAsync(
            new SuppliedBriefAgentInput(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Supplier availability request",
                content,
                Array.Empty<BriefClarificationInput>()),
            CancellationToken.None);

        Assert.Contains("1 October 2026", result.Draft.Timing, StringComparison.Ordinal);
        Assert.Contains("31 December 2026", result.Draft.Timing, StringComparison.Ordinal);
        Assert.DoesNotContain("16 July 2026", result.Draft.Timing, StringComparison.Ordinal);
    }
}
