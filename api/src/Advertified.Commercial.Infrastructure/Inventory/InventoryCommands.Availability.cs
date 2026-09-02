using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> RecordAvailabilityExceptionOutcomeAsync(
        Guid productId,
        CommandEnvelope<RecordInventoryAvailabilityExceptionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var type = command.ExceptionType?.Trim().ToUpperInvariant();
        if (command.ProductVersionId == Guid.Empty || command.EndsOn < command.StartsOn ||
            string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("The availability exception is invalid.");
        }
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, MasterDataCodes.AvailabilityExceptionTypes.Collection,
            type, cancellationToken);
        var locator = OpportunityCommandSupport.Required(
            command.SourceLocator, 1_000, nameof(command.SourceLocator));
        var hash = command.EvidenceHash?.Trim().ToLowerInvariant();
        if (hash is null || hash.Length != 64 || hash.Any(value => !Uri.IsHexDigit(value)))
        {
            throw new ArgumentException("A valid evidence hash is required.");
        }
        var product = await store.DbContext.Database.SqlQuery<AvailabilityProductRow>($"""
            SELECT id AS "Id", current_version_id AS "CurrentVersionId", version AS "Version"
            FROM commercial.inventory_products
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        if (product.Version != envelope.ExpectedVersion ||
            product.CurrentVersionId != command.ProductVersionId)
        {
            throw new VersionConflictException();
        }
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_availability_exceptions (
                id, tenant_id, product_id, product_version_id, exception_type_code,
                starts_on, ends_on, source_locator, evidence_hash,
                recorded_by, recorded_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {productId},
                {command.ProductVersionId}, {type},
                {command.StartsOn}, {command.EndsOn}, {locator}, {hash},
                {envelope.ActorId.Value}, {now});
            UPDATE commercial.inventory_products
            SET version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND version = {envelope.ExpectedVersion};
            """, cancellationToken);
        var view = new InventoryAvailabilityExceptionView(
            id, productId, command.ProductVersionId, type,
            command.StartsOn, command.EndsOn, locator, hash,
            envelope.ActorId.Value, now, 1);
        return OpportunityCommandSupport.Outcome(
            envelope, view, productId, product.Version + 1,
            MasterDataReferences.CommercialResourceTypes.InventoryProduct,
            MasterDataReferences.CommercialActions.InventoryAvailabilityExceptionRecorded,
            MasterDataReferences.CommercialEventTypes.InventoryAvailabilityExceptionRecorded,
            now);
    }

    private sealed record AvailabilityProductRow(
        Guid Id,
        Guid CurrentVersionId,
        long Version);
}
