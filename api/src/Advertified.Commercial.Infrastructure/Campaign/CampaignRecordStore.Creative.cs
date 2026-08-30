using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore
{
    internal async Task RequestCreativeAsync(
        CampaignRow row,
        CommandEnvelope<RequestCampaignCreativeCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.CreativePending},
                creative_requested_by = {envelope.ActorId.Value},
                creative_requested_at_utc = {now}, creative_request_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Booked}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    internal async Task ApproveCreativeAsync(
        CampaignRow row,
        CommandEnvelope<ApproveCampaignCreativeCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Ready},
                creative_approved_by = {envelope.ActorId.Value},
                creative_approved_at_utc = {now}, creative_approval_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.CreativePending}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
