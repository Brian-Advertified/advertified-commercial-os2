using Advertified.Commercial.Application.Brief;

namespace Advertified.Commercial.Infrastructure.Brief;

internal static class BriefAudienceResearchValidation
{
    internal static void Validate(CreateBriefVersionCommand command)
    {
        var research = command.AudienceResearch ?? [];
        if (research.Count > 20) throw new ArgumentException("At most twenty audience research records are allowed.");
        foreach (var item in research)
        {
            if (item is null) throw new ArgumentException("Audience research records cannot be empty.");
            Required(item.AudienceName, 300);
            if (!command.Audiences.Contains(item.AudienceName, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Audience research must identify a supplied audience.");
            Required(item.SourceLocator, 2000);
            Required(item.SourceExcerpt, 4000);
            Required(item.MeasurementPeriod, 200);
            Required(item.Methodology, 1000);
            Optional(item.Language, 100); Optional(item.LifeStage, 200);
            Optional(item.LsmSem, 100); Optional(item.LsmSemTaxonomy, 200);
            Optional(item.LsmSemTaxonomyVersion, 100); Optional(item.NeedState, 1000);
            Optional(item.BuyingContext, 500); Optional(item.MessageContext, 200);
            Optional(item.MomentContext, 200);
            if (item.LsmSem is not null &&
                (item.LsmSemTaxonomy is null || item.LsmSemTaxonomyVersion is null))
                throw new ArgumentException("Segmentation research needs its exact taxonomy and version.");
        }
    }

    private static void Required(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
            throw new ArgumentException("Audience research needs a bounded source, excerpt, period and methodology.");
    }

    private static void Optional(string? value, int limit)
    {
        if (value is not null) Required(value, limit);
    }
}
