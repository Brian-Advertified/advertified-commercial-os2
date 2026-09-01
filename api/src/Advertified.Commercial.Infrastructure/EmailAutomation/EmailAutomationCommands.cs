using System.Net.Mail;
using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationCommands(
    EmailAutomationRecordStore store,
    CommandDispatcher dispatcher,
    ITenantAuthorizer authorizer,
    IEmailProposalAutomationProcessor processor,
    EmailAutomationPolicy policy,
    IOptions<EmailAutomationOptions> options,
    TimeProvider timeProvider) : IEmailAutomationCommands
{
    public async Task<CommandResult<InboundMailboxView>> ConfigureMailboxAsync(
        CommandEnvelope<ConfigureInboundMailboxCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EmailAutomationManage,
            token => ConfigureOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InboundMailboxView>(receipt);
    }

    public async Task<CommandResult<InboundEmailReceiptView>> ReceiveAsync(
        CommandEnvelope<ReceiveInboundEmailCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EmailAutomationExecute,
            token => ReceiveOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InboundEmailReceiptView>(receipt);
    }

    public async Task<CommandResult<EmailAutomationRunView>> ProcessAsync(
        Guid inboundEmailId,
        CommandEnvelope<ProcessInboundEmailCommand> envelope,
        CancellationToken cancellationToken)
    {
        var existing = await ReadAuthorizedRunAsync(
            inboundEmailId, envelope, cancellationToken);
        if (existing.Status == MasterDataCodes.EmailAutomationStatuses.Sent)
        {
            return new CommandResult<EmailAutomationRunView>(
                EmailAutomationRecordStore.ToView(existing), existing.Version, false);
        }
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EmailAutomationManage,
            token => PrepareProcessOutcomeAsync(inboundEmailId, envelope, token),
            cancellationToken);
        return await ContinuePreparedRunAsync(
            inboundEmailId, envelope, receipt, cancellationToken);
    }

    public async Task<CommandResult<EmailAutomationRunView>> RetryAsync(
        Guid inboundEmailId,
        CommandEnvelope<RetryInboundEmailCommand> envelope,
        CancellationToken cancellationToken)
    {
        _ = Required(
            envelope.Command.Reason,
            policy.MaximumRetryReasonLength,
            nameof(envelope.Command.Reason));
        var clarifications = NormalizeClarifications(
            envelope.Command.Clarifications ?? []);
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EmailAutomationManage,
            token => PrepareRetryOutcomeAsync(
                inboundEmailId, envelope, clarifications, token),
            cancellationToken);
        return await ContinuePreparedRunAsync(
            inboundEmailId, envelope, receipt, cancellationToken);
    }

    private EmailAutomationClarificationInput[] NormalizeClarifications(
        IReadOnlyList<EmailAutomationClarificationInput> clarifications)
    {
        if (clarifications.Count > policy.MaximumClarificationCount)
        {
            throw new ArgumentException("Too many clarification answers were supplied.");
        }
        var normalized = clarifications.Select(item => new EmailAutomationClarificationInput(
                Required(
                    item.FieldPath,
                    policy.MaximumClarificationLength,
                    nameof(item.FieldPath)),
                Required(
                    item.Value,
                    policy.MaximumClarificationLength,
                    nameof(item.Value))))
            .ToArray();
        if (normalized.Select(item => item.FieldPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            throw new ArgumentException("Each clarification field may be answered once.");
        }
        return normalized;
    }

    private static void EnsureClarificationsAllowed(
        EmailAutomationRunRow run,
        EmailAutomationClarificationInput[] clarifications)
    {
        if (run.FailureCode == MasterDataCodes.AutomationFailureReasons.IncompleteBrief &&
            clarifications.Length == 0)
        {
            throw new ArgumentException("Answer the unclear Brief details before retrying.");
        }
        if (clarifications.Length == 0)
        {
            return;
        }
        if (run.FailureCode != MasterDataCodes.AutomationFailureReasons.IncompleteBrief ||
            run.BriefVersionId.HasValue ||
            string.IsNullOrWhiteSpace(run.UnderstandingJson))
        {
            throw new ArgumentException(
                "Clarifications may only resolve an incomplete Brief before planning starts.");
        }
        var outstanding = EmailAutomationRecordStore.Read<SuppliedBriefUnderstandingView>(
                run.UnderstandingJson)
            .Questions
            .Where(item => item.IsBlocking)
            .Select(item => item.FieldPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clarifications.Any(item => !outstanding.Contains(item.FieldPath)))
        {
            throw new ArgumentException("A clarification does not match an outstanding question.");
        }
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Email automation access denied.");
        }
    }

    private static string Required(string value, int maximumLength, string parameter)
    {
        var result = value.Trim();
        if (result.Length == 0 || result.Length > maximumLength)
        {
            throw new ArgumentException("A valid email automation value is required.", parameter);
        }
        return result;
    }

    private static string NormalizeAddress(string value, string parameter)
    {
        try
        {
            return new MailAddress(Required(value, 320, parameter)).Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("A valid email address is required.", parameter, exception);
        }
    }

    private static string[] NormalizeDomains(IReadOnlyList<string> values) =>
        values.Select(value => value.Trim().TrimStart('@').ToLowerInvariant())
            .Where(value => value.Length > 0 && value.Length <= 253 && value.Contains('.'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void ValidateJsonObject(string value)
    {
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Inbound email metadata must be a JSON object.");
        }
    }
}
