using Advertified.Commercial.Application.Intelligence;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class AudienceEvidenceGuard
{
    internal static void Validate(
        IReadOnlyList<AudienceDefinitionProposal> audiences,
        IReadOnlyList<AudienceEvidenceFact> evidence)
    {
        foreach (var audience in audiences)
        {
            var facts = evidence.Where(item => string.Equals(item.AudienceName,
                audience.Name, StringComparison.OrdinalIgnoreCase) &&
                (item.BriefVersionId.HasValue || item.EvidenceItemId.HasValue)).ToArray();
            var supportedEvidenceIds = facts
                .Where(item => item.EvidenceItemId.HasValue)
                .Select(item => item.EvidenceItemId!.Value)
                .ToHashSet();
            if (audience.EvidenceItemIds.Any(item => !supportedEvidenceIds.Contains(item)))
                throw new InvalidOperationException("Audience evidence IDs must be explicitly bound to that audience.");
            Check(audience.Language, facts.Select(item => item.Language));
            Check(audience.LifeStage, facts.Select(item => item.LifeStage));
            Check(audience.LsmSem, facts.Select(item => item.LsmSem));
            Check(audience.LsmSemTaxonomy, facts.Select(item => item.LsmSemTaxonomy));
            Check(audience.LsmSemTaxonomyVersion, facts.Select(item => item.LsmSemTaxonomyVersion));
        }
    }

    private static void Check(string? value, IEnumerable<string?> candidates)
    {
        if (value is null) return;
        var supplied = candidates.Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (supplied.Length != 1 || supplied[0] != value)
            throw new InvalidOperationException("Audience attributes must match approved audience evidence.");
    }
}
