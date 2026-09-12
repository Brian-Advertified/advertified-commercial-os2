using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AudienceProposalValidationTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void CandidateCountAllowsExplicitNoAudienceOutcome(int count, bool allowed)
    {
        var audiences = Enumerable.Range(1, count)
            .Select(index => Candidate($"Supplied audience {index}"))
            .ToArray();
        var error = Record.Exception(() =>
            AudienceIntelligenceValidator.Validate(audiences, [], []));
        if (allowed) Assert.Null(error);
        else Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void CaseAndWhitespaceDoNotCreateDistinctCandidates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AudienceIntelligenceValidator.Validate(
                [Candidate("Furniture buyers"), Candidate(" furniture BUYERS ")], [], []));
    }

    [Fact]
    public void UnsupportedPsychologyMayRemainStructurallyUnknown()
    {
        var candidate = Candidate("Furniture buyers");

        AudienceIntelligenceValidator.Validate([candidate], [], []);

        Assert.Null(candidate.NeedState);
        Assert.Null(candidate.BuyingContext);
        Assert.Null(candidate.Confidence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredAudienceCannotBeOmittedOrReplacedByAnUnrelatedHypothesis(bool empty)
    {
        var audiences = empty ? Array.Empty<AudienceDefinitionProposal>() : [Candidate("Unrelated hypothesis")];
        Assert.Throws<InvalidOperationException>(() => AudienceIntelligenceValidator.Validate(
            audiences, [], [], clientRequiredAudiences: ["Unseen equipment procurement team"]));
    }

    [Fact]
    public void RequiredAudienceAndGeographyAreEnforcedAtTheCanonicalBoundary()
    {
        var audience = Candidate(" unseen procurement TEAM ") with
        {
            Classification = MasterDataCodes.EvidenceClassifications.ClientRequirement,
        };
        Assert.Throws<InvalidOperationException>(() => AudienceIntelligenceValidator.Validate(
            [audience], ["Approved district"], [], clientRequiredAudiences: ["Unseen procurement team"]));
        AudienceIntelligenceValidator.Validate([audience with { Geographies = ["Approved district"] }],
            ["Approved district"], [], clientRequiredAudiences: ["Unseen procurement team"]);
    }

    [Fact]
    public void ApprovedEvidenceIdentifierDoesNotLicenseInventedAudienceAttributes()
    {
        var evidenceId = Guid.NewGuid();
        var source = new AudienceEvidenceFact(evidenceId, "Unseen procurement team", "Source language",
            "Source life stage", "Source segment", "Source taxonomy", "2026", null, null, null, null);
        var proposal = Candidate(source.AudienceName) with
        {
            Classification = MasterDataCodes.EvidenceClassifications.Inference,
            EvidenceItemIds = [evidenceId],
            Language = source.Language,
            LifeStage = source.LifeStage,
            LsmSem = source.LsmSem,
            LsmSemTaxonomy = source.LsmSemTaxonomy,
            LsmSemTaxonomyVersion = source.LsmSemTaxonomyVersion,
        };
        AudienceIntelligenceValidator.Validate([proposal], [], [evidenceId], [source]);
        var invalid = new[]
        {
            proposal with { Language = "Invented language" },
            proposal with { LifeStage = "Invented life stage" },
            proposal with { LsmSemTaxonomyVersion = "Unseen taxonomy version" },
            proposal with { Name = "A different audience" },
            proposal with { EvidenceItemIds = [] },
        };
        foreach (var candidate in invalid)
            Assert.Throws<InvalidOperationException>(() =>
                AudienceIntelligenceValidator.Validate([candidate], [], [evidenceId], [source]));
    }

    private static AudienceDefinitionProposal Candidate(string name) => new(
        name,
        "Audience hypothesis retained without unsupported motivations or behaviours.",
        null,
        null,
        [],
        null,
        null,
        null,
        null,
        null,
        MasterDataCodes.EvidenceClassifications.Hypothesis,
        [],
        [],
        [],
        null,
        false);
}
