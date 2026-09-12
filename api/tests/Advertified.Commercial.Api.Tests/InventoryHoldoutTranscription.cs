using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

internal static class InventoryHoldoutTranscription
{
    internal static void Verify(IReadOnlyList<InventorySemanticPacket> packets, string sourceHash)
    {
        var results = packets.Select(packet => Result(packet, forged: false)).ToArray();
        var rows = InventorySemanticTranscriptionMerger.Merge(packets, results);
        Assert.Equal(packets.Sum(packet => packet.SourceItems.Count), rows.Count);
        foreach (var row in rows)
        {
            var candidate = InventoryCandidateNormalizer.Normalize(row, sourceHash,
                new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero));
            Assert.Null(candidate.SupplierName);
            Assert.Null(candidate.Values.RateAmountMinor);
            Assert.Null(candidate.Values.CommercialTerms?.RateValidFrom);
            Assert.Null(candidate.Values.CommercialTerms?.RateValidTo);
            var prepared = InventoryExtractionCompletionPolicy.PrepareCandidate(
                candidate, "Not supplied", InventoryHoldoutProjection.Codes());
            Assert.True(InventoryCandidateReviewPolicy.RequiresReview(prepared));
            Assert.Contains(candidate.Evidence, evidence =>
                evidence.RawValue == row.Values["name"] && evidence.SourceLocator == row.Locator);
        }
        // Grounded transcription cannot smuggle an amount absent from the source.
        var forged = packets.Select(packet => Result(packet, forged: true)).ToArray();
        Assert.Throws<InventorySemanticResultRejectedException>(() =>
            InventorySemanticTranscriptionMerger.Merge(packets, forged));
    }

    private static AgentSemanticResult Result(InventorySemanticPacket packet, bool forged)
    {
        var candidates = packet.SourceItems.Select(item =>
        {
            var fields = new List<ProposedInventoryField>
            {
                Field("name", item.Content, item.Locator),
            };
            if (forged) fields.Add(Field("rate", "987654321.09", item.Locator));
            return new ProposedInventoryCandidate(item.Locator, fields,
                ["Record identity and commercial meanings require human review."]);
        }).ToArray();
        return new(packet.InputHash, new AgentRuntimeResponse<InventorySemanticExtractionArtifact>
        {
            SchemaVersion = "1.0.0",
            Status = "REVIEW_REQUIRED",
            Artifact = new(candidates, []),
            EvidenceBindings = [], Unknowns = [], Assumptions = [],
            Confidence = [], Objections = [],
            Rationale = "Synthetic source-bound transcription boundary fixture.",
            Usage = new("deterministic", "fixture-v1", 0, 0, 0, "FIXTURE"),
        });
    }

    private static ProposedInventoryField Field(string name, string raw, string locator) =>
        new(name, raw, null, locator, MasterDataCodes.InventoryEvidenceBases.SupplierSupplied,
            MasterDataCodes.InventoryTransformationTypes.Trim, 1m);
}
