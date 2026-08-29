using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Foundation;

public sealed record UpdateTenantCommand(
    string LegalName,
    string TradingName,
    string SettingsJson);

public sealed record UpdateUserCommand(
    string DisplayName,
    string? Phone);

public sealed record CreateClientAccountCommand(
    string ExternalReference,
    string LegalName,
    string TradingName,
    string? Website,
    string? Industry,
    string BillingProfileJson);

public sealed record CreateAgencyCommand(
    string ExternalReference,
    string LegalName,
    string TradingName,
    string? Website);

public sealed record CreateContactCommand(
    Guid ClientAccountId,
    string Name,
    string? JobTitle,
    string Email,
    string? Phone,
    string PurposeCode,
    string ConsentBasis,
    DateOnly? RetainUntil);

public interface ICommercialFoundationReader
{
    Task<TenantView> GetTenantAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<CursorPage<ClientAccountView>> ListClientAccountsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<CursorPage<MembershipView>> ListMembershipsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<CursorPage<AgencyView>> ListAgenciesAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<CursorPage<ContactView>> ListContactsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);
}

public interface IIdentityFoundationCommands
{
    Task<CommandResult<TenantView>> UpdateTenantAsync(
        CommandEnvelope<UpdateTenantCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<Identity.CurrentUserView>> UpdateUserAsync(
        CommandEnvelope<UpdateUserCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IBusinessFoundationCommands
{
    Task<CommandResult<ClientAccountView>> CreateClientAccountAsync(
        CommandEnvelope<CreateClientAccountCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<AgencyView>> CreateAgencyAsync(
        CommandEnvelope<CreateAgencyCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ContactView>> CreateContactAsync(
        CommandEnvelope<CreateContactCommand> envelope,
        CancellationToken cancellationToken);
}
