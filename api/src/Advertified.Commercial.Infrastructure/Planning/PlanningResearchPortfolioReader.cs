using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningResearchPortfolioReader
{
    internal static async Task<CampaignResearchPortfolioEvidence[]> ReadAsync(
        GovernanceDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<CampaignResearchPortfolioRow>($"""
            SELECT portfolio.id AS "Id", portfolio.product_version_ids_json::text AS "ProductVersionIdsJson",
                portfolio.deduplicated_reach AS "DeduplicatedReach", portfolio.unit_code AS "Unit",
                dataset.source_name AS "MeasurementSource", dataset.measurement_period AS "MeasurementPeriod",
                dataset.methodology AS "Methodology", dataset.universe AS "Universe"
            FROM commercial.inventory_research_portfolios portfolio
            JOIN commercial.inventory_research_datasets dataset
              ON dataset.tenant_id = portfolio.tenant_id AND dataset.id = portfolio.dataset_id
            WHERE portfolio.tenant_id = {tenantId.Value}
              AND portfolio.status_code = {MasterDataCodes.LifecycleStatuses.Completed}
              AND portfolio.product_version_ids_json IS NOT NULL
            ORDER BY portfolio.id
            """).ToArrayAsync(cancellationToken);
        return rows.Select(ToEvidence).ToArray();
    }

    private static CampaignResearchPortfolioEvidence ToEvidence(CampaignResearchPortfolioRow row)
    {
        var ids = JsonSerializer.Deserialize<Guid[]>(row.ProductVersionIdsJson) ?? [];
        return new(row.Id, ids.Order().ToArray(), row.DeduplicatedReach, row.Unit,
            row.Universe, row.MeasurementPeriod, row.MeasurementSource, row.Methodology);
    }

    private sealed record CampaignResearchPortfolioRow(
        Guid Id, string ProductVersionIdsJson, decimal DeduplicatedReach, string Unit,
        string MeasurementSource, string MeasurementPeriod, string Methodology, string Universe);
}

internal sealed record CampaignResearchPortfolioEvidence(
    Guid Id,
    IReadOnlyList<Guid> ProductVersionIds,
    decimal DeduplicatedReach,
    string Unit,
    string Universe,
    string MeasurementPeriod,
    string MeasurementSource,
    string Methodology);
