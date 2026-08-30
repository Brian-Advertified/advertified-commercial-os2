using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationCommands
{
    private async Task<CommandOutcome> ConfigureOutcomeAsync(
        CommandEnvelope<ConfigureInboundMailboxCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        if (command.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Mailbox ownership must match the accountable user.");
        }
        var address = NormalizeAddress(command.Address, nameof(command.Address));
        var provider = Required(command.Provider, 100, nameof(command.Provider)).ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext,
            MasterDataCodes.EmailProviders.Collection,
            provider,
            cancellationToken);
        await EnsureMailboxReferencesAsync(envelope, cancellationToken);
        var domains = NormalizeDomains(command.AllowedSenderDomains);
        if (command.AllowedSenderDomains.Count > 0 && domains.Length == 0)
        {
            throw new ArgumentException("At least one valid sender domain is required.");
        }
        var existing = await store.FindMailboxAsync(envelope.TenantId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        InboundMailboxRow persisted;
        if (existing is null)
        {
            if (envelope.ExpectedVersion != 0)
            {
                throw new VersionConflictException();
            }
            var id = Guid.NewGuid();
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inbound_mailboxes (
                    id, tenant_id, address, provider_code, owner_user_id,
                    default_client_account_id, auto_send_enabled,
                    allowed_sender_domains_json, is_enabled, version,
                    created_at_utc, updated_at_utc)
                VALUES ({id}, {envelope.TenantId.Value}, {address}, {provider},
                    {command.OwnerUserId}, {command.DefaultClientAccountId},
                    {command.AutoSendEnabled}, {EmailAutomationRecordStore.Write(domains)}::jsonb,
                    true, 1, {now}, {now})
                """, cancellationToken);
            persisted = new InboundMailboxRow(
                id, envelope.TenantId.Value, address, provider, command.OwnerUserId,
                command.DefaultClientAccountId, command.AutoSendEnabled,
                EmailAutomationRecordStore.Write(domains), true, 1, now, now);
        }
        else
        {
            if (envelope.ExpectedVersion != existing.Version)
            {
                throw new VersionConflictException();
            }
            var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inbound_mailboxes
                SET address = {address}, provider_code = {provider},
                    owner_user_id = {command.OwnerUserId},
                    default_client_account_id = {command.DefaultClientAccountId},
                    auto_send_enabled = {command.AutoSendEnabled},
                    allowed_sender_domains_json = {EmailAutomationRecordStore.Write(domains)}::jsonb,
                    is_enabled = true, version = version + 1,
                    updated_at_utc = {now}
                WHERE tenant_id = {envelope.TenantId.Value} AND id = {existing.Id}
                  AND version = {envelope.ExpectedVersion}
                """, cancellationToken);
            if (changed != 1)
            {
                throw new VersionConflictException();
            }
            persisted = existing with
            {
                Address = address,
                Provider = provider,
                OwnerUserId = command.OwnerUserId,
                DefaultClientAccountId = command.DefaultClientAccountId,
                AutoSendEnabled = command.AutoSendEnabled,
                AllowedSenderDomainsJson = EmailAutomationRecordStore.Write(domains),
                IsEnabled = true,
                Version = existing.Version + 1,
                UpdatedAtUtc = now,
            };
        }
        var view = EmailAutomationRecordStore.ToView(persisted);
        return OpportunityCommandSupport.Outcome(
            envelope,
            view,
            persisted.Id,
            persisted.Version,
            MasterDataReferences.CommercialResourceTypes.InboundMailbox,
            MasterDataReferences.CommercialActions.InboundMailboxConfigured,
            MasterDataReferences.CommercialEventTypes.InboundMailboxConfigured,
            now);
    }

    private Task<bool> HasOwnerMembershipAsync(
        CommandEnvelope<ConfigureInboundMailboxCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.memberships
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND user_id = {envelope.Command.OwnerUserId}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
                  AND role_code = ANY({new[]
                    {
                        MasterDataCodes.Roles.PlatformAdmin,
                        MasterDataCodes.Roles.AgencyAdmin,
                    }})) AS "Value"
            """).SingleAsync(cancellationToken);

    private async Task EnsureMailboxReferencesAsync(
        CommandEnvelope<ConfigureInboundMailboxCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (!await HasOwnerMembershipAsync(envelope, cancellationToken))
        {
            throw new UnauthorizedAccessException("Mailbox owner access denied.");
        }
        if (!envelope.Command.DefaultClientAccountId.HasValue)
        {
            return;
        }
        var clientExists = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.client_accounts
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND id = {envelope.Command.DefaultClientAccountId.Value}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.Active}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!clientExists)
        {
            throw new UnauthorizedAccessException("Default client access denied.");
        }
    }
}
