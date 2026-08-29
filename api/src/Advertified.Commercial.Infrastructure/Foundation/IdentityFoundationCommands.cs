using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Foundation;

public sealed class IdentityFoundationCommands(
    GovernanceDbContext dbContext,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IIdentityFoundationCommands
{
    public async Task<CommandResult<TenantView>> UpdateTenantAsync(
        CommandEnvelope<UpdateTenantCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate2Permissions.TenantManage,
            async token =>
            {
                var tenant = await dbContext.Tenants.SingleOrDefaultAsync(
                    item => item.Id == envelope.TenantId,
                    token) ?? throw new UnauthorizedAccessException("Tenant access denied.");
                var now = timeProvider.GetUtcNow();
                tenant.UpdateProfile(
                    envelope.Command.LegalName,
                    envelope.Command.TradingName,
                    envelope.Command.SettingsJson,
                    envelope.ExpectedVersion,
                    now);
                var view = FoundationViewMapper.ToView(tenant);
                return CommandOutcomeFactory.Create(
                    envelope,
                    view,
                    tenant.Id.Value,
                    tenant.Version,
                    CommercialResourceTypes.Tenant,
                    CommercialActions.TenantUpdated,
                    CommercialEventTypes.TenantUpdated,
                    now);
            },
            cancellationToken);
        return CommandOutcomeFactory.ToResult<TenantView>(receipt);
    }

    public async Task<CommandResult<CurrentUserView>> UpdateUserAsync(
        CommandEnvelope<UpdateUserCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate2Permissions.UserManageSelf,
            async token =>
            {
                var userId = new UserId(envelope.ActorId.Value);
                var user = await dbContext.Users.SingleOrDefaultAsync(
                    item => item.Id == userId,
                    token) ?? throw new UnauthorizedAccessException("Identity access denied.");
                var now = timeProvider.GetUtcNow();
                user.UpdateProfile(
                    envelope.Command.DisplayName,
                    envelope.Command.Phone,
                    envelope.ExpectedVersion,
                    now);
                var view = FoundationViewMapper.ToView(user);
                return CommandOutcomeFactory.Create(
                    envelope,
                    view,
                    user.Id.Value,
                    user.Version,
                    CommercialResourceTypes.User,
                    CommercialActions.UserUpdated,
                    CommercialEventTypes.UserUpdated,
                    now);
            },
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CurrentUserView>(receipt);
    }
}
