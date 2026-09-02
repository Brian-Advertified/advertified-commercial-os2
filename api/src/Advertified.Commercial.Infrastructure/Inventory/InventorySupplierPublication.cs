using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record PreparedSupplierPublication(
    Guid VersionId,
    InventorySupplierCommercialValues? Commercial,
    IReadOnlyList<InventorySupplierContactValue> Contacts);

internal static class InventorySupplierPublication
{
    internal static PreparedSupplierPublication Prepare(
        IReadOnlyList<ApprovedInventoryCandidate> candidates)
    {
        var supplied = candidates.Select(item => item.Values.SupplierCommercial)
            .Where(item => item is not null).Select(item => item!).ToArray();
        var commercial = supplied.Length == 0 ? null : new InventorySupplierCommercialValues(
            One(supplied.Select(item => item.VatStatus)),
            One(supplied.Select(item => item.VatNumber)),
            One(supplied.Select(item => item.CommissionTerms)),
            One(supplied.Select(item => item.PaymentTerms)),
            One(supplied.Select(item => item.CancellationTerms)),
            One(supplied.Select(item => item.BookingDeadlineTerms)));
        var contacts = candidates.SelectMany(item => item.Values.SupplierContacts ?? [])
            .GroupBy(ContactKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToArray();
        return new(Guid.NewGuid(), commercial, contacts);
    }

    internal static async Task PersistAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        Guid publishedBy,
        DateTimeOffset now,
        PreparedSupplierPublication publication,
        CancellationToken cancellationToken)
    {
        if (publication.Commercial is not null)
        {
            var value = publication.Commercial;
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_supplier_versions (
                    id, tenant_id, supplier_id, version_number, vat_status_code,
                    vat_number, commission_terms, payment_terms, cancellation_terms,
                    booking_deadline_terms, source_import_id, published_by, published_at_utc)
                SELECT {publication.VersionId}, {tenantId.Value}, {supplierId},
                    COALESCE(MAX(version_number), 0) + 1, {value.VatStatus}, {value.VatNumber},
                    {value.CommissionTerms}, {value.PaymentTerms}, {value.CancellationTerms},
                    {value.BookingDeadlineTerms}, {importId}, {publishedBy}, {now}
                FROM commercial.inventory_supplier_versions
                WHERE tenant_id = {tenantId.Value} AND supplier_id = {supplierId};
                UPDATE commercial.inventory_suppliers
                SET current_commercial_version_id = {publication.VersionId},
                    version = version + 1, updated_at_utc = {now}
                WHERE tenant_id = {tenantId.Value} AND id = {supplierId};
                """, cancellationToken);
        }
        foreach (var contact in publication.Contacts)
        {
            await InsertContactAsync(
                dbContext, tenantId, supplierId, importId, now, contact, cancellationToken);
        }
    }

    private static Task<int> InsertContactAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        DateTimeOffset now,
        InventorySupplierContactValue value,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_supplier_contacts (
                id, tenant_id, supplier_id, name, role, region, email, phone,
                website, social_handle, source_import_id, observed_at_utc)
            SELECT {Guid.NewGuid()}, {tenantId.Value}, {supplierId}, {value.Name}, {value.Role},
                {value.Region}, {value.Email}, {value.Phone}, {value.Website},
                {value.SocialHandle}, {importId}, {now}
            WHERE NOT EXISTS (
                SELECT 1 FROM commercial.inventory_supplier_contacts existing
                WHERE existing.tenant_id = {tenantId.Value}
                  AND existing.supplier_id = {supplierId}
                  AND existing.name IS NOT DISTINCT FROM {value.Name}
                  AND existing.email IS NOT DISTINCT FROM {value.Email}
                  AND existing.phone IS NOT DISTINCT FROM {value.Phone}
                  AND existing.website IS NOT DISTINCT FROM {value.Website}
                  AND existing.social_handle IS NOT DISTINCT FROM {value.SocialHandle});
            """, cancellationToken);

    private static string ContactKey(InventorySupplierContactValue value) =>
        value.Email ?? value.Phone ?? value.Website ?? value.SocialHandle ?? value.Name ??
        throw new InventoryPublishBlockedException();

    private static string? One(IEnumerable<string?> values)
    {
        var distinct = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return distinct.Length switch
        {
            0 => null,
            1 => distinct[0],
            _ => throw new InventoryPublishBlockedException(),
        };
    }
}
