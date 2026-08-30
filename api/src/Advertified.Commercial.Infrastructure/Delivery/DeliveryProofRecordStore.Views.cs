using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore
{
    internal Task<DeliveryProofRow?> FindAsync(
        Guid proofId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = ProofSelect + " WHERE proof.id = {0}" +
            (forUpdate ? " FOR UPDATE OF proof" : string.Empty);
        return DbContext.Database.SqlQuery<DeliveryProofRow>(
            FormattableStringFactory.Create(sql, proofId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<DeliveryProofRow>> ListCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var sql = ProofSelect +
            " WHERE proof.campaign_id = {0} ORDER BY proof.submitted_at_utc, proof.id";
        return DbContext.Database.SqlQuery<DeliveryProofRow>(
            FormattableStringFactory.Create(sql, campaignId))
            .ToListAsync(cancellationToken);
    }
}
