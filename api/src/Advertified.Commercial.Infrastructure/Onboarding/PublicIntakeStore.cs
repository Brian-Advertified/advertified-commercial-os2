using System.Runtime.CompilerServices;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Onboarding;

public sealed class PublicIntakeStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    private const string Projection = """
        SELECT id AS "Id", type_code AS "TypeCode", name AS "Name",
            email AS "Email", phone AS "Phone", organisation AS "Organisation",
            website AS "Website", relationship AS "Relationship", message AS "Message",
            status_code AS "Status", created_at_utc AS "CreatedAtUtc",
            reviewed_by AS "ReviewedBy", reviewed_at_utc AS "ReviewedAtUtc",
            review_reason AS "ReviewReason", provisioned_tenant_id AS "ProvisionedTenantId",
            provisioned_user_id AS "ProvisionedUserId", version AS "Version"
        FROM governance.public_intake_requests
        """;

    internal async Task<PublicIntakeRow> InsertAsync(
        NormalizedPublicIntake request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(dbContext, null, null, cancellationToken);
        var registered = await dbContext.MasterDataItems.AnyAsync(item =>
            item.CollectionCode == MasterDataCodes.PublicIntakeTypes.Collection &&
            item.Code == request.TypeCode && item.IsActive,
            cancellationToken);
        if (!registered) throw new ArgumentException("The intake type is not active.");
        var id = Guid.NewGuid();
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO governance.public_intake_requests (
                id, type_code, name, email, phone, organisation, website,
                relationship, message, status_code, created_at_utc, version)
            VALUES ({id}, {request.TypeCode}, {request.Name}, {request.Email},
                {request.Phone}, {request.Organisation}, {request.Website},
                {request.Relationship}, {request.Message},
                {MasterDataCodes.LifecycleStatuses.Pending}, {now}, 1)
            """, cancellationToken);
        if (changed != 1) throw new InvalidOperationException("The public request was not recorded.");
        var row = await FindAsync(id, false, cancellationToken)
            ?? throw new InvalidOperationException("The public request was not recorded.");
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    internal async Task<IReadOnlyList<PublicIntakeRow>> ListAsync(
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var take = limit + 1;
        if (string.IsNullOrWhiteSpace(status))
        {
            return await Query(
                " ORDER BY created_at_utc DESC, id DESC LIMIT {0} OFFSET {1}",
                take,
                offset).ToListAsync(cancellationToken);
        }
        return await Query(
            " WHERE status_code = {0} ORDER BY created_at_utc DESC, id DESC LIMIT {1} OFFSET {2}",
            status.Trim().ToUpperInvariant(),
            take,
            offset).ToListAsync(cancellationToken);
    }

    internal Task<PublicIntakeRow?> FindAsync(
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken) =>
        Query(
            forUpdate ? " WHERE id = {0} FOR UPDATE" : " WHERE id = {0}",
            id).SingleOrDefaultAsync(cancellationToken);

    internal async Task<PublicIntakeRow> MarkProvisionedAsync(
        PublicIntakeRow row,
        Guid reviewerId,
        Guid tenantId,
        Guid userId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE governance.public_intake_requests
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved},
                reviewed_by = {reviewerId}, reviewed_at_utc = {now},
                review_reason = {reason}, provisioned_tenant_id = {tenantId},
                provisioned_user_id = {userId}, version = version + 1
            WHERE id = {row.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND version = {row.Version}
            """, cancellationToken);
        if (changed != 1) throw new InvalidOperationException("The intake request changed before it was provisioned.");
        return await FindAsync(row.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The intake request was not found after provisioning.");
    }

    internal async Task<PublicIntakeRow> MarkRejectedAsync(
        PublicIntakeRow row,
        Guid reviewerId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE governance.public_intake_requests
            SET status_code = {MasterDataCodes.LifecycleStatuses.Rejected},
                reviewed_by = {reviewerId}, reviewed_at_utc = {now},
                review_reason = {reason}, version = version + 1
            WHERE id = {row.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND version = {row.Version}
            """, cancellationToken);
        if (changed != 1) throw new InvalidOperationException("The intake request changed before it was reviewed.");
        return await FindAsync(row.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The intake request was not found after review.");
    }

    internal async Task<PublicIntakeRow> MarkResolvedAsync(
        PublicIntakeRow row,
        Guid reviewerId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE governance.public_intake_requests
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                reviewed_by = {reviewerId}, reviewed_at_utc = {now},
                review_reason = {reason}, version = version + 1
            WHERE id = {row.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND version = {row.Version}
            """, cancellationToken);
        if (changed != 1) throw new InvalidOperationException("The intake request changed before it was resolved.");
        return await FindAsync(row.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The intake request was not found after review.");
    }

    internal static (int Limit, int Offset) ParsePage(int limit, string? cursor) =>
        CursorPageFactory.Parse(limit, cursor);

    internal static Advertified.Commercial.Application.Foundation.CursorPage<T> Page<T>(
        IReadOnlyList<T> rows,
        int limit,
        int offset) => CursorPageFactory.Create(rows, limit, offset);

    private IQueryable<PublicIntakeRow> Query(string suffix, params object[] arguments) =>
        dbContext.Database.SqlQuery<PublicIntakeRow>(
            FormattableStringFactory.Create(Projection + suffix, arguments));
}
