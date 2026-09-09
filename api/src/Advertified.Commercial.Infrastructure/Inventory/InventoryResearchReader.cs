using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryResearchReader(
    GovernanceDbContext dbContext,
    ITenantAuthorizer authorizer) : IInventoryResearchReader
{
    public async Task<InventoryResearchDatasetView> GetAsync(
        ActorId actorId, TenantId tenantId, Guid datasetId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryReview, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Research access denied.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        var view = await InventoryResearchProjection.BuildAsync(
            new InventoryResearchStore(dbContext), tenantId, datasetId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }
}

internal static class InventoryResearchProjection
{
    internal static async Task<InventoryResearchDatasetView> BuildAsync(
        InventoryResearchStore store, TenantId tenantId, Guid datasetId,
        CancellationToken cancellationToken)
    {
        var dataset = await store.FindDatasetAsync(tenantId, datasetId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Research dataset access denied.");
        var matches = await store.ListMatchesAsync(tenantId, datasetId, cancellationToken);
        var portfolios = await store.ListPortfoliosAsync(tenantId, datasetId, cancellationToken);
        return new(dataset.Id, dataset.SourceName, dataset.DatasetName,
            dataset.MeasurementPeriod, dataset.Methodology, dataset.Universe,
            dataset.RightsReference, dataset.TaxonomyName, dataset.TaxonomyVersion,
            dataset.Limitations, dataset.Status, dataset.CreatedBy, dataset.CreatedAtUtc,
            dataset.Version, matches.Select(ToView).ToArray(), portfolios.Select(ToView).ToArray());
    }

    private static InventoryResearchMatchView ToView(InventoryResearchMatchRow row)
    {
        var profile = JsonSerializer.Deserialize<InventoryAudienceProfileValues>(
            row.AudienceProfileJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored research audience profile is invalid.");
        return new(row.Id, row.ObservationId, row.SourceLocator, row.ProductId,
            row.ProductVersionId, row.ProductName, row.MatchBasis, row.MatchDetail,
            profile, row.Status, row.ReviewedBy, row.ReviewedAtUtc,
            row.AppliedProductVersionId, row.Version);
    }

    private static InventoryResearchPortfolioView ToView(InventoryResearchPortfolioRow row)
    {
        var locators = JsonSerializer.Deserialize<string[]>(
            row.ObservationSourceLocatorsJson, InventoryRowMapper.StoredJson) ?? [];
        var products = string.IsNullOrWhiteSpace(row.ProductVersionIdsJson)
            ? [] : JsonSerializer.Deserialize<Guid[]>(row.ProductVersionIdsJson,
                InventoryRowMapper.StoredJson) ?? [];
        return new(row.Id, row.SourceLocator, locators, row.DeduplicatedReach,
            row.Unit, products, row.Status);
    }
}
