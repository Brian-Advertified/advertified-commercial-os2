using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeRecordStore
{
    internal async Task InsertRequirementsAsync<TCommand>(
        Guid campaignId,
        CommandEnvelope<TCommand> envelope,
        IReadOnlyList<PreparedCreativeRequirement> requirements,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        foreach (var item in requirements)
        {
            var source = item.Source;
            var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.creative_requirements (
                    id, buyer_tenant_id, supplier_tenant_id, campaign_id, booking_id,
                    media_plan_line_id, channel_code, flight_start, flight_end,
                    format_code, width, height, required_media_type, maximum_bytes,
                    instructions, created_by, created_at_utc)
                VALUES ({item.Id}, {envelope.TenantId.Value}, {source.SupplierTenantId},
                    {campaignId}, {source.BookingId}, {source.MediaPlanLineId}, {source.Channel},
                    {source.FlightStart}, {source.FlightEnd}, {item.FormatCode}, {item.Width},
                    {item.Height}, {item.RequiredMediaType}, {item.MaximumBytes},
                    {item.Instructions}, {envelope.ActorId.Value}, {now})
                ON CONFLICT (buyer_tenant_id, campaign_id, booking_id) DO NOTHING
                """, cancellationToken);
            if (changed != 1) throw new CreativeReadinessBlockedException();
        }
    }
}
