using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore
{
    internal async Task StartAsync(
        CampaignRow row,
        CommandEnvelope<StartCampaignCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Live},
                started_by = {envelope.ActorId.Value}, started_at_utc = {now},
                start_reason = {reason}, version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Ready}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    internal async Task CompleteAsync(
        CampaignRow row,
        CommandEnvelope<CompleteCampaignCommand> envelope,
        string completionReason,
        string proofRequestReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                completed_by = {envelope.ActorId.Value}, completed_at_utc = {now},
                completion_reason = {completionReason},
                proof_requested_by = {envelope.ActorId.Value}, proof_requested_at_utc = {now},
                proof_request_reason = {proofRequestReason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Live}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
