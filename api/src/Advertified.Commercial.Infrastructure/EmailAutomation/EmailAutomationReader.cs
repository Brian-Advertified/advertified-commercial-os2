using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailAutomationReader(
    EmailAutomationRecordStore store,
    ITenantAuthorizer authorizer) : IEmailAutomationReader
{
    public async Task<InboundMailboxView?> GetMailboxAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindMailboxAsync(tenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row is null ? null : EmailAutomationRecordStore.ToView(row);
    }

    public async Task<InboundEmailPage> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        var limit = pageSize is >= 1 and <= 100 ? pageSize : 25;
        var before = EmailAutomationRecordStore.DecodeCursor(cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListEmailsAsync(
            tenantId, limit, before, cancellationToken);
        var selected = rows.Take(limit).ToArray();
        var views = new List<InboundCampaignEmailView>(selected.Length);
        foreach (var row in selected)
        {
            var run = await store.FindRunAsync(tenantId, row.Id, cancellationToken)
                ?? throw new InvalidOperationException("The inbound email has no automation run.");
            views.Add(await store.BuildEmailViewAsync(
                tenantId, row, run.Status, run.FailureCode, cancellationToken));
        }
        await transaction.CommitAsync(cancellationToken);
        var next = rows.Count > limit
            ? EmailAutomationRecordStore.EncodeCursor(selected[^1].ReceivedAtUtc)
            : null;
        return new InboundEmailPage(views, next);
    }

    public async Task<InboundEmailDetailView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var email = await store.FindEmailAsync(
            tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        var run = await store.FindRunAsync(tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        var emailView = await store.BuildEmailViewAsync(
            tenantId, email, run.Status, run.FailureCode, cancellationToken);
        var questions = ReadQuestions(run.UnderstandingJson);
        await transaction.CommitAsync(cancellationToken);
        return new InboundEmailDetailView(
            emailView,
            EmailAutomationRecordStore.ToView(run),
            email.BodyText,
            questions);
    }

    private static EmailAutomationQuestionView[] ReadQuestions(string? understandingJson)
    {
        if (string.IsNullOrWhiteSpace(understandingJson))
        {
            return [];
        }
        var understanding = EmailAutomationRecordStore.Read<SuppliedBriefUnderstandingView>(
            understandingJson);
        return understanding.Questions
            .Where(item => item.IsBlocking)
            .Select(item => new EmailAutomationQuestionView(
                item.FieldPath,
                item.Question,
                item.Options))
            .ToArray();
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.EmailAutomationView,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Email automation access denied.");
        }
    }
}
