using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AudienceResearchEvidenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructuredAttributesRequireTheExactReviewedAudience(bool directBrief)
    {
        var id = Guid.NewGuid();
        var fact = new AudienceEvidenceFact(directBrief ? null : id, "Parents", "English",
            "Preschool parents", "8", "Synthetic SEM", "fixture-1", null, null, null, null)
            { BriefVersionId = directBrief ? Guid.NewGuid() : null };
        var audience = new AudienceDefinitionProposal("Parents", "Supplied audience", "Unknown",
            "Unknown", [], "English", "Preschool parents", "8", "Synthetic SEM", "fixture-1",
            MasterDataCodes.EvidenceClassifications.Inference, [], directBrief ? [] : [id], 0.5m, true);
        PlanningAudienceProposalValidator.Validate([audience], [], directBrief ? [] : [id], [fact]);
        PlanningAudienceEvidenceGuard.Validate([audience], [fact]);
        Assert.Throws<InvalidOperationException>(() => PlanningAudienceEvidenceGuard.Validate(
            [audience with { Language = "Invented" }], [fact]));
        Assert.Throws<InvalidOperationException>(() => PlanningAudienceEvidenceGuard.Validate(
            [audience with { Name = "Different audience" }], [fact]));
        Assert.Throws<InvalidOperationException>(() => PlanningAudienceEvidenceGuard.Validate(
            [audience], [fact, fact with { Language = "Conflicting source" }]));
    }

    [Fact]
    public void EvidenceProjectionDoesNotInferPersonaAttributesOrIncompleteTaxonomy()
    {
        var parsed = PlanningAudienceEvidenceReader.Parse(Guid.NewGuid(), """
            {"audienceName":"Stay-at-home mothers","lsmSem":"8","language":"English"}
            """, ["Stay-at-home mothers"]);
        Assert.NotNull(parsed);
        Assert.Equal("English", parsed.Language);
        Assert.Null(parsed.LifeStage);
        Assert.Null(parsed.LsmSem);
        Assert.Null(PlanningAudienceEvidenceReader.Parse(Guid.NewGuid(),
            """{"audienceName":"Unrelated audience","language":"English"}""", ["Parents"]));
    }
}
