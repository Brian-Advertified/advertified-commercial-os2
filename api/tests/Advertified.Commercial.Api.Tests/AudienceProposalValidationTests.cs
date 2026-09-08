using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AudienceProposalValidationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void CandidateCountMatchesSuppliedAudienceContract(int count, bool allowed)
    {
        var audiences = Enumerable.Range(1, count)
            .Select(index => Candidate($"Supplied audience {index}")).ToArray();
        var error = Record.Exception(() =>
            PlanningAudienceProposalValidator.Validate(audiences, [], []));
        if (allowed) Assert.Null(error);
        else Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void CaseAndWhitespaceDoNotCreateDistinctCandidates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PlanningAudienceProposalValidator.Validate(
                [Candidate("Furniture buyers"), Candidate(" furniture BUYERS ")], [], []));
    }

    private static AudienceDefinitionProposal Candidate(string name) => new(
        name, "Supplied audience label; media habits require research.",
        "Not supplied.", "Not supplied.", [], null, null, null, null, null,
        MasterDataCodes.EvidenceClassifications.Hypothesis, [], [], 0m, true);
}
