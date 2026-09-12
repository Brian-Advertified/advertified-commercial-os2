using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PlanningDecisionContextTests
{
    [Fact]
    public void ReusesApprovedBriefMeasurementAudienceDirectionAndMediaRoles()
    {
        var briefVersionId = Guid.NewGuid();
        var audienceId = Guid.NewGuid();
        var brief = Brief(
            briefVersionId,
            "Store visits have declined.",
            "Increase qualified store visits.",
            "[\"Store visits\",\"Qualified enquiries\"]",
            500_000,
            false,
            "ZAR",
            3);
        var audience = new AudienceStrategyView(
            audienceId,
            briefVersionId,
            1,
            [],
            "Prioritise high-intent local buyers.",
            "Make the offer easy to act on near the buying moment.",
            "fixture",
            "APPROVED",
            [],
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        var mix = new MediaMixVersionView(
            Guid.NewGuid(),
            briefVersionId,
            audience.Id,
            Guid.NewGuid(),
            1,
            500_000,
            "ZAR",
            [
                new("OOH", 300_000, "Build local salience", []),
                new("RADIO", 200_000, "Explain the offer", []),
            ],
            [],
            "fixture",
            "APPROVED",
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTimeOffset.UnixEpoch);

        var result = PlanningDecisionContext.Build(brief, audience, mix);

        Assert.Equal("Store visits have declined.", result.BusinessProblem);
        Assert.Equal("Increase qualified store visits.", result.Objective);
        Assert.Equal(["Store visits", "Qualified enquiries"], result.SuccessMeasures);
        Assert.Equal("Prioritise high-intent local buyers.", result.TargetingRationale);
        Assert.Equal("Make the offer easy to act on near the buying moment.", result.PositioningStatement);
        Assert.Collection(result.MediaJobs,
            item => Assert.Equal(("OOH", "Build local salience", 300_000L),
                (item.Channel, item.Role, item.BudgetMinor)),
            item => Assert.Equal(("RADIO", "Explain the offer", 200_000L),
                (item.Channel, item.Role, item.BudgetMinor)));
        Assert.Empty(result.EvidenceGaps);
    }

    [Fact]
    public void MissingCanonicalInputsRemainVisibleAsGapsInsteadOfBeingInvented()
    {
        var brief = Brief(
            Guid.NewGuid(),
            "Problem",
            "Objective",
            "[]",
            null,
            true,
            null,
            1);

        var result = PlanningDecisionContext.Build(brief, null, null);

        Assert.Empty(result.SuccessMeasures);
        Assert.Contains("commercialFlow.successMeasureMissing", result.EvidenceGaps);
        Assert.Contains("commercialFlow.audienceStrategyMissing", result.EvidenceGaps);
        Assert.Contains("commercialFlow.mediaJobsMissing", result.EvidenceGaps);
    }

    private static PlanningBriefRow Brief(
        Guid id,
        string problem,
        string objective,
        string measurementJson,
        long? budgetMinor,
        bool budgetUnknown,
        string? currency,
        long version) => new(
            Id: id,
            TenantId: Guid.NewGuid(),
            BriefId: Guid.NewGuid(),
            ClientName: "Fixture client",
            OwnerUserId: Guid.NewGuid(),
            Status: "APPROVED",
            BusinessProblem: problem,
            Objective: objective,
            AudiencesJson: "[]",
            GeographiesJson: "[]",
            MediaRequirementsJson: "[]",
            ConstraintsJson: "[]",
            ConflictsJson: "[]",
            MeasurementJson: measurementJson,
            BudgetMinor: budgetMinor,
            BudgetUnknown: budgetUnknown,
            Currency: currency,
            VatStatus: null,
            FeesMinor: null,
            EvidenceIdsJson: "[]",
            Version: version);
}
