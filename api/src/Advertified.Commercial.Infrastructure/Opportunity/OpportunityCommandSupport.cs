using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal static class OpportunityCommandSupport
{
    private const string EmptyJson = "{}";
    private const string CompletedJson = "{\"completed\":true}";

    public static string Required(string value, int maximum, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        return normalized.Length <= maximum
            ? normalized
            : throw new ArgumentOutOfRangeException(parameterName);
    }

    public static string? Optional(string? value, int maximum, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, maximum, parameterName);
    }

    public static string Json(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("A valid JSON value is required.", parameterName, exception);
        }
    }

    public static CapturedSource Capture(RegisterEvidenceSourceCommand command)
    {
        var type = Required(command.Type, 100, nameof(command.Type)).ToUpperInvariant();
        if (type == Gate4SourceTypes.SuppliedText)
        {
            var content = Required(command.Content ?? string.Empty, 262_144, nameof(command.Content));
            return new CapturedSource(type, content, command.Claims);
        }

        if (type == Gate4SourceTypes.PermittedUrl)
        {
            var locator = ValidatePermittedUrl(command.Locator);
            if (!string.Equals(
                    locator,
                    Gate4SourceTypes.DeterministicFixtureUrl,
                    StringComparison.Ordinal))
            {
                throw new CaptureProviderDisabledException();
            }
            const string fixture =
                "Local fixture business supplies modular workspace furniture to small teams in Gauteng.";
            var claims = command.Claims.Count > 0
                ? command.Claims
                : [new CandidateEvidenceCommand(
                    "fixture:paragraph:1",
                    Gate4EvidenceCodes.BusinessContext,
                    "{\"statement\":\"Modular workspace furniture for small Gauteng teams\"}",
                    fixture,
                    1m)];
            return new CapturedSource(type, fixture, claims);
        }

        throw new CaptureProviderDisabledException();
    }

    private static string ValidatePermittedUrl(string locator)
    {
        var value = Required(locator, 2048, nameof(locator));
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.IsDefaultPort || uri.HostNameType != UriHostNameType.Dns)
        {
            throw new ArgumentException("The source URL is not permitted.", nameof(locator));
        }
        return uri.AbsoluteUri;
    }

    public static string Hash(string content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public static async Task EnsureCodeAsync(
        GovernanceDbContext dbContext,
        string collection,
        string code,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.MasterDataItems.AnyAsync(
            item => item.CollectionCode == collection && item.Code == code && item.IsActive,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("A governed code is invalid.", nameof(code));
        }
    }

    public static async Task CreateTaskAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid opportunityId,
        string taskType,
        string title,
        string whyItMatters,
        ResourceTypeCode resourceType,
        Guid resourceId,
        long resourceVersion,
        Guid assigneeUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var taskId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES (
                {taskId}, {tenantId.Value}, {opportunityId}, {taskType}, {Gate4Statuses.Pending},
                {title}, {whyItMatters}, {resourceType.Value}, {resourceId}, {resourceVersion},
                {assigneeUserId}, {EmptyJson}::jsonb, 1, {now})
            """, cancellationToken);
    }

    public static Task CompleteTaskAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid resourceId,
        string taskType,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks
            SET status_code = {Gate4Statuses.Completed}, completed_by = {actorId},
                completed_at_utc = {now}, completion_json = {CompletedJson}::jsonb,
                version = version + 1
            WHERE tenant_id = {tenantId.Value} AND resource_id = {resourceId}
              AND task_type_code = {taskType} AND assignee_user_id = {actorId}
              AND status_code = {Gate4Statuses.Pending}
            """, cancellationToken);

    public static CommandOutcome Outcome<TCommand, TResult>(
        CommandEnvelope<TCommand> envelope,
        TResult view,
        Guid resourceId,
        long version,
        ResourceTypeCode resourceType,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull
        where TResult : notnull =>
        CommandOutcomeFactory.Create(
            envelope, view, resourceId, version, resourceType, action, eventType, now);

    public static async Task EnsureDifferentActiveReviewerAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid creatorId,
        Guid reviewerId,
        string[] allowedRoles,
        CancellationToken cancellationToken)
    {
        if (creatorId == reviewerId)
        {
            throw new ApprovalRequiredException();
        }

        var active = await dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.memberships
                WHERE tenant_id = {tenantId.Value} AND user_id = {reviewerId}
                  AND status_code = {Gate4Statuses.Active}
                  AND role_code = ANY({allowedRoles})) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!active)
        {
            throw new ApprovalRequiredException();
        }
    }
}

internal sealed record CapturedSource(
    string Type,
    string Content,
    IReadOnlyList<CandidateEvidenceCommand> Claims);
