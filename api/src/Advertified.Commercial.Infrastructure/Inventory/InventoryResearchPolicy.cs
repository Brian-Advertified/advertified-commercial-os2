using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryResearchPolicy
{
    internal static void Validate(RegisterInventoryResearchDatasetCommand command)
    {
        Required(command.SourceName, 300);
        Required(command.DatasetName, 300);
        Required(command.MeasurementPeriod, 200);
        Required(command.Methodology, 2_000);
        Required(command.Universe, 1_000);
        Required(command.RightsReference, 1_000);
        Optional(command.TaxonomyName, 200);
        Optional(command.TaxonomyVersion, 100);
        Optional(command.Limitations, 2_000);
        if (command.Observations.Count is < 1 or > 500)
            throw new ArgumentException("Research datasets require 1 to 500 observations.");
        if (command.Observations.Select(item => item.SourceLocator.Trim())
            .Distinct(StringComparer.Ordinal).Count() != command.Observations.Count)
            throw new ArgumentException("Research observation source locators must be unique.");
        foreach (var observation in command.Observations) ValidateObservation(command, observation);
        ValidatePortfolios(command);
    }

    internal static string ReviewDecision(string decision) => decision switch
    {
        MasterDataCodes.InventoryReviewDecisions.Approve => decision,
        MasterDataCodes.InventoryReviewDecisions.Reject => decision,
        _ => throw new ArgumentException("Research match decision must be APPROVE or REJECT."),
    };

    internal static string ReviewReason(string value) => Required(value, 2_000);

    private static void ValidateObservation(
        RegisterInventoryResearchDatasetCommand dataset,
        InventoryResearchObservationInput observation)
    {
        Required(observation.SourceLocator, 1_000);
        Optional(observation.Channel, 100);
        Optional(observation.SupplierProductCode, 200);
        Optional(observation.OutletId, 64);
        Optional(observation.Geography, 500);
        if (string.IsNullOrWhiteSpace(observation.OutletId) &&
            (!observation.SupplierId.HasValue || string.IsNullOrWhiteSpace(observation.SupplierProductCode)))
            throw new ArgumentException("Each research observation requires a canonical outlet id or supplier/product identity.");
        var profile = observation.AudienceProfile;
        if (!Same(profile.MeasurementSource, dataset.SourceName) ||
            !Same(profile.MeasurementPeriod, dataset.MeasurementPeriod) ||
            !Same(profile.Methodology, dataset.Methodology) ||
            !Same(profile.Universe, dataset.Universe) ||
            !SameOptional(profile.TaxonomyName, dataset.TaxonomyName) ||
            !SameOptional(profile.TaxonomyVersion, dataset.TaxonomyVersion))
            throw new ArgumentException("Research observation provenance must match its dataset metadata.");
        if (profile.Measurements is not { Count: > 0 } || profile.Measurements.Any(item =>
                !item.Value.HasValue || string.IsNullOrWhiteSpace(item.Unit) ||
                !Same(item.MeasurementSource, dataset.SourceName) ||
                !Same(item.MeasurementPeriod, dataset.MeasurementPeriod) ||
                !Same(item.Methodology, dataset.Methodology) ||
                !Same(item.Universe, dataset.Universe)))
            throw new ArgumentException("Research observations require complete compatible delivery measurements.");
    }

    private static void ValidatePortfolios(RegisterInventoryResearchDatasetCommand command)
    {
        var portfolios = command.Portfolios ?? [];
        if (portfolios.Count > 200)
            throw new ArgumentException("Research datasets support at most 200 portfolio measurements.");
        var observations = command.Observations.ToDictionary(
            item => item.SourceLocator.Trim(), StringComparer.Ordinal);
        if (portfolios.Select(item => item.SourceLocator.Trim())
            .Distinct(StringComparer.Ordinal).Count() != portfolios.Count)
            throw new ArgumentException("Research portfolio source locators must be unique.");
        foreach (var portfolio in portfolios)
        {
            Required(portfolio.SourceLocator, 1_000);
            if (portfolio.ObservationSourceLocators.Count < 2 ||
                portfolio.ObservationSourceLocators.Distinct(StringComparer.Ordinal).Count() !=
                    portfolio.ObservationSourceLocators.Count ||
                portfolio.ObservationSourceLocators.Any(locator => !observations.ContainsKey(locator.Trim())))
                throw new ArgumentException("Portfolio measurements require at least two distinct registered observations.");
            if (portfolio.DeduplicatedReach < 0 ||
                portfolio.Unit != MasterDataCodes.MeasurementUnits.People)
                throw new ArgumentException("Deduplicated reach must be a non-negative PEOPLE measurement.");
            var gross = portfolio.ObservationSourceLocators.Sum(locator =>
                Reach(observations[locator.Trim()].AudienceProfile));
            if (portfolio.DeduplicatedReach > gross)
                throw new ArgumentException("Deduplicated reach cannot exceed the sum of compatible placement reach.");
        }
    }

    private static decimal Reach(InventoryAudienceProfileValues profile) =>
        (profile.Measurements ?? []).FirstOrDefault(item =>
            item.MetricType == MasterDataCodes.PerformanceMetricTypes.Reach &&
            item.Unit == MasterDataCodes.MeasurementUnits.People)?.Value ??
        throw new ArgumentException("Portfolio measurements require PEOPLE reach on every referenced observation.");

    private static bool Same(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static bool SameOptional(string? left, string? right) =>
        string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right) ||
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static string Required(string value, int maximum)
    {
        var result = value.Trim();
        if (result.Length is < 1 || result.Length > maximum) throw new ArgumentException("Research text is invalid.");
        return result;
    }

    private static void Optional(string? value, int maximum)
    {
        if (value?.Trim().Length > maximum) throw new ArgumentException("Research text is too long.");
    }
}
