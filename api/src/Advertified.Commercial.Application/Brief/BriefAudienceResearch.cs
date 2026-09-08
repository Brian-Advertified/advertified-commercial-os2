namespace Advertified.Commercial.Application.Brief;

public sealed record BriefAudienceResearch(
    string AudienceName, string SourceLocator, string SourceExcerpt,
    string MeasurementPeriod, string Methodology,
    string? Language = null, string? LifeStage = null, string? LsmSem = null,
    string? LsmSemTaxonomy = null, string? LsmSemTaxonomyVersion = null,
    string? NeedState = null, string? BuyingContext = null,
    string? MessageContext = null, string? MomentContext = null);
