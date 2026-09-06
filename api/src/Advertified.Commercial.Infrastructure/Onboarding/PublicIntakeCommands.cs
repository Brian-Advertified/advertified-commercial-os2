using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Onboarding;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Onboarding;

public sealed class PublicIntakeCommands(
    PublicIntakeStore store,
    PublicIntakeProvisioner provisioner,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IPublicIntakeCommands
{
    public async Task<PublicIntakeView> SubmitAsync(
        SubmitPublicIntakeRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PublicIntakePolicy.Normalize(request);
        var row = await store.InsertAsync(
            normalized, timeProvider.GetUtcNow(), cancellationToken);
        return row.ToView();
    }

    public Task<CommandResult<PublicIntakeView>> ProvisionAsync(
        Guid requestId,
        CommandEnvelope<ProvisionPublicIntakeCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope,
            token => ProvisionOutcomeAsync(requestId, envelope, token),
            cancellationToken);

    public Task<CommandResult<PublicIntakeView>> RejectAsync(
        Guid requestId,
        CommandEnvelope<RejectPublicIntakeCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope,
            token => ReviewOutcomeAsync(
                requestId, envelope, envelope.Command.Reason, false, token),
            cancellationToken);

    public Task<CommandResult<PublicIntakeView>> ResolveAsync(
        Guid requestId,
        CommandEnvelope<ResolvePublicIntakeCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope,
            token => ReviewOutcomeAsync(
                requestId, envelope, envelope.Command.Reason, true, token),
            cancellationToken);

    private async Task<CommandResult<PublicIntakeView>> DispatchAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.PublicIntakeManage,
            execute,
            cancellationToken);
        return CommandOutcomeFactory.ToResult<PublicIntakeView>(receipt);
    }

    private async Task<CommandOutcome> ProvisionOutcomeAsync(
        Guid requestId,
        CommandEnvelope<ProvisionPublicIntakeCommand> envelope,
        CancellationToken cancellationToken)
    {
        await EnsurePlatformTenantAsync(envelope, cancellationToken);
        var row = await store.FindAsync(requestId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Public intake request access denied.");
        EnsurePending(row, envelope.ExpectedVersion);
        var command = envelope.Command;
        var legalName = PublicIntakePolicy.LegalName(command.LegalName);
        var tradingName = PublicIntakePolicy.TradingName(command.TradingName);
        var website = PublicIntakePolicy.NormalizeWebsite(command.Website);
        var vatNumber = PublicIntakePolicy.NormalizeVatNumber(command.VatNumber);
        var reason = PublicIntakePolicy.ReviewReason(command.Reason);
        var now = timeProvider.GetUtcNow();
        var provisioned = await provisioner.ProvisionAsync(
            row,
            envelope.ActorId,
            envelope.TenantId,
            legalName,
            tradingName,
            website,
            vatNumber,
            command.RequireMfa,
            now,
            cancellationToken);
        var updated = await store.MarkProvisionedAsync(
            row,
            envelope.ActorId.Value,
            provisioned.TenantId,
            provisioned.UserId,
            reason,
            now,
            cancellationToken);
        return Outcome(
            envelope,
            updated.ToView(),
            MasterDataReferences.CommercialActions.PublicIntakeProvisioned,
            MasterDataReferences.CommercialEventTypes.PublicIntakeProvisioned,
            now);
    }

    private async Task<CommandOutcome> ReviewOutcomeAsync<TCommand>(
        Guid requestId,
        CommandEnvelope<TCommand> envelope,
        string suppliedReason,
        bool resolve,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        await EnsurePlatformTenantAsync(envelope, cancellationToken);
        var row = await store.FindAsync(requestId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Public intake request access denied.");
        EnsurePending(row, envelope.ExpectedVersion);
        if (resolve && !PublicIntakePolicy.IsEnquiry(row.TypeCode))
            throw new InvalidOperationException("Registration requests must be provisioned or rejected.");
        var reason = PublicIntakePolicy.ReviewReason(suppliedReason);
        var now = timeProvider.GetUtcNow();
        var updated = resolve
            ? await store.MarkResolvedAsync(row, envelope.ActorId.Value, reason, now, cancellationToken)
            : await store.MarkRejectedAsync(row, envelope.ActorId.Value, reason, now, cancellationToken);
        return Outcome(
            envelope,
            updated.ToView(),
            resolve ? MasterDataReferences.CommercialActions.PublicIntakeResolved : MasterDataReferences.CommercialActions.PublicIntakeRejected,
            resolve ? MasterDataReferences.CommercialEventTypes.PublicIntakeResolved : MasterDataReferences.CommercialEventTypes.PublicIntakeRejected,
            now);
    }

    private async Task EnsurePlatformTenantAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var isPlatform = await store.DbContext.Tenants.AnyAsync(
            tenant => tenant.Id == envelope.TenantId &&
                tenant.Type == new TenantTypeCode(MasterDataCodes.TenantTypes.Platform),
            cancellationToken);
        if (!isPlatform)
            throw new UnauthorizedAccessException("Public intake access denied.");
    }

    private static void EnsurePending(PublicIntakeRow row, long expectedVersion)
    {
        if (row.Status != MasterDataCodes.LifecycleStatuses.Pending ||
            row.Version != expectedVersion)
            throw new InvalidOperationException("The public intake request changed before review.");
    }

    private static CommandOutcome Outcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PublicIntakeView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope,
            view,
            view.Id,
            view.Version,
            MasterDataReferences.CommercialResourceTypes.PublicIntakeRequest,
            action,
            eventType,
            now,
            auditMetadata: JsonSerializer.SerializeToElement(new
            {
                view.TypeCode,
                view.ProvisionedTenantId,
                view.ProvisionedUserId,
            }));
}
