using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventoryResearchObservationInput(
    string SourceLocator,
    string? Channel,
    Guid? SupplierId,
    string? SupplierProductCode,
    string? OutletId,
    string? Geography,
    InventoryAudienceProfileValues AudienceProfile);

public sealed record InventoryResearchPortfolioInput(
    string SourceLocator,
    IReadOnlyList<string> ObservationSourceLocators,
    decimal DeduplicatedReach,
    string Unit);

public sealed record RegisterInventoryResearchDatasetCommand(
    string SourceName,
    string DatasetName,
    string MeasurementPeriod,
    string Methodology,
    string Universe,
    string RightsReference,
    string? TaxonomyName,
    string? TaxonomyVersion,
    string? Limitations,
    IReadOnlyList<InventoryResearchObservationInput> Observations,
    IReadOnlyList<InventoryResearchPortfolioInput>? Portfolios = null);

public sealed record ReviewInventoryResearchMatchCommand(
    string Decision,
    string Reason);

public sealed record InventoryResearchMatchView(
    Guid Id,
    Guid ObservationId,
    string SourceLocator,
    Guid? ProductId,
    Guid? ProductVersionId,
    string? ProductName,
    string? MatchBasis,
    string MatchDetail,
    InventoryAudienceProfileValues AudienceProfile,
    string Status,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    Guid? AppliedProductVersionId,
    long Version);

public sealed record InventoryResearchPortfolioView(
    Guid Id,
    string SourceLocator,
    IReadOnlyList<string> ObservationSourceLocators,
    decimal DeduplicatedReach,
    string Unit,
    IReadOnlyList<Guid> ProductVersionIds,
    string Status);

public sealed record InventoryResearchDatasetView(
    Guid Id,
    string SourceName,
    string DatasetName,
    string MeasurementPeriod,
    string Methodology,
    string Universe,
    string RightsReference,
    string? TaxonomyName,
    string? TaxonomyVersion,
    string? Limitations,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    long Version,
    IReadOnlyList<InventoryResearchMatchView> Matches,
    IReadOnlyList<InventoryResearchPortfolioView> Portfolios);

public interface IInventoryResearchCommands
{
    Task<CommandResult<InventoryResearchDatasetView>> RegisterAsync(
        CommandEnvelope<RegisterInventoryResearchDatasetCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryResearchDatasetView>> ReviewAsync(
        Guid matchId,
        CommandEnvelope<ReviewInventoryResearchMatchCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IInventoryResearchReader
{
    Task<InventoryResearchDatasetView> GetAsync(
        ActorId actorId, TenantId tenantId, Guid datasetId,
        CancellationToken cancellationToken);
}
