using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Foundation;

public sealed class BusinessFoundationCommands(
    GovernanceDbContext dbContext,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IBusinessFoundationCommands
{
    public async Task<CommandResult<ClientAccountView>> CreateClientAccountAsync(
        CommandEnvelope<CreateClientAccountCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.ClientAccountManage,
            token => CreateClientAccountOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<ClientAccountView>(receipt);
    }

    public async Task<CommandResult<AgencyView>> CreateAgencyAsync(
        CommandEnvelope<CreateAgencyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.AgencyManage,
            token => CreateAgencyOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<AgencyView>(receipt);
    }

    public async Task<CommandResult<ContactView>> CreateContactAsync(
        CommandEnvelope<CreateContactCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.ContactManage,
            token => CreateContactOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<ContactView>(receipt);
    }

    private Task<CommandOutcome> CreateClientAccountOutcomeAsync(
        CommandEnvelope<CreateClientAccountCommand> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = timeProvider.GetUtcNow();
        var command = envelope.Command;
        var entity = new ClientAccount(
            new ClientAccountId(Guid.NewGuid()),
            envelope.TenantId,
            command.ExternalReference,
            command.LegalName,
            command.TradingName,
            command.Website,
            command.Industry,
            command.BillingProfileJson,
            MasterDataReferences.LifecycleStatuses.Active,
            now);
        dbContext.ClientAccounts.Add(entity);
        return Task.FromResult(CreateOutcome(
            envelope,
            FoundationViewMapper.ToView(entity),
            entity.Id.Value,
            MasterDataReferences.CommercialResourceTypes.ClientAccount,
            MasterDataReferences.CommercialActions.ClientAccountCreated,
            MasterDataReferences.CommercialEventTypes.ClientAccountCreated,
            now));
    }

    private Task<CommandOutcome> CreateAgencyOutcomeAsync(
        CommandEnvelope<CreateAgencyCommand> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = timeProvider.GetUtcNow();
        var command = envelope.Command;
        var entity = new Agency(
            new AgencyId(Guid.NewGuid()),
            envelope.TenantId,
            command.ExternalReference,
            command.LegalName,
            command.TradingName,
            command.Website,
            MasterDataReferences.LifecycleStatuses.Active,
            now);
        dbContext.Agencies.Add(entity);
        return Task.FromResult(CreateOutcome(
            envelope,
            FoundationViewMapper.ToView(entity),
            entity.Id.Value,
            MasterDataReferences.CommercialResourceTypes.Agency,
            MasterDataReferences.CommercialActions.AgencyCreated,
            MasterDataReferences.CommercialEventTypes.AgencyCreated,
            now));
    }

    private async Task<CommandOutcome> CreateContactOutcomeAsync(
        CommandEnvelope<CreateContactCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var clientAccountId = new ClientAccountId(command.ClientAccountId);
        var purpose = new ContactPurposeCode(command.PurposeCode);
        await EnsureContactReferencesAsync(
            envelope.TenantId,
            clientAccountId,
            purpose,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var entity = new Contact(
            new ContactId(Guid.NewGuid()),
            envelope.TenantId,
            clientAccountId,
            command.Name,
            command.JobTitle,
            new EmailAddress(command.Email),
            command.Phone,
            purpose,
            command.ConsentBasis,
            command.RetainUntil,
            MasterDataReferences.LifecycleStatuses.Active,
            now);
        dbContext.Contacts.Add(entity);
        return CreateOutcome(
            envelope,
            FoundationViewMapper.ToView(entity),
            entity.Id.Value,
            MasterDataReferences.CommercialResourceTypes.Contact,
            MasterDataReferences.CommercialActions.ContactCreated,
            MasterDataReferences.CommercialEventTypes.ContactCreated,
            now);
    }

    private async Task EnsureContactReferencesAsync(
        TenantId tenantId,
        ClientAccountId clientAccountId,
        ContactPurposeCode purpose,
        CancellationToken cancellationToken)
    {
        var clientExists = await dbContext.ClientAccounts.AnyAsync(
            item => item.TenantId == tenantId && item.Id == clientAccountId,
            cancellationToken);
        if (!clientExists)
        {
            throw new UnauthorizedAccessException("Client account access denied.");
        }

        var purposeExists = await dbContext.MasterDataItems.AnyAsync(
            item => item.CollectionCode == MasterDataCodes.ContactPurposes.Collection
                && item.Code == purpose.Value
                && item.IsActive,
            cancellationToken);
        if (!purposeExists)
        {
            throw new ArgumentException("The contact purpose is invalid.", nameof(purpose));
        }
    }

    private static CommandOutcome CreateOutcome<TCommand, TResult>(
        CommandEnvelope<TCommand> envelope,
        TResult view,
        Guid resourceId,
        ResourceTypeCode resourceType,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull
        where TResult : notnull
    {
        return CommandOutcomeFactory.Create(
            envelope,
            view,
            resourceId,
            1,
            resourceType,
            action,
            eventType,
            now);
    }
}
