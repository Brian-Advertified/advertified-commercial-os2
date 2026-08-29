using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityCommands
{
    public async Task<CommandResult<OpportunityView>> UpdateAsync(
        Guid opportunityId,
        CommandEnvelope<UpdateOpportunityCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.OpportunityEdit,
            token => UpdateOutcomeAsync(opportunityId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<OpportunityView>(receipt);
    }

    private async Task<CommandOutcome> UpdateOutcomeAsync(
        Guid opportunityId,
        CommandEnvelope<UpdateOpportunityCommand> envelope,
        CancellationToken cancellationToken)
    {
        var current = await EnsureOwnerAsync(envelope, opportunityId, cancellationToken);
        if (current.Stage == MasterDataCodes.LifecycleStatuses.BriefReady)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var command = envelope.Command;
        var title = OpportunityCommandSupport.Required(
            command.Title, 200, nameof(command.Title));
        var currency = await ValidateMoneyAsync(command, cancellationToken);
        var problem = OpportunityCommandSupport.Optional(
            command.ProblemSummary, 2000, nameof(command.ProblemSummary));
        var objective = OpportunityCommandSupport.Optional(
            command.ObjectiveSummary, 2000, nameof(command.ObjectiveSummary));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunities
            SET title = {title}, expected_value_minor = {command.ExpectedValueMinor},
                currency_code = {currency}, deadline = {command.Deadline},
                problem_summary = {problem}, objective_summary = {objective},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {opportunityId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var view = current with
        {
            Title = title,
            ExpectedValueMinor = command.ExpectedValueMinor,
            Currency = currency,
            Deadline = command.Deadline,
            ProblemSummary = problem,
            ObjectiveSummary = objective,
            Version = current.Version + 1,
            UpdatedAtUtc = now,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), opportunityId, view.Version,
            MasterDataReferences.CommercialResourceTypes.Opportunity, MasterDataReferences.CommercialActions.OpportunityUpdated,
            MasterDataReferences.CommercialEventTypes.OpportunityUpdated, now);
    }

    private async Task<string?> ValidateMoneyAsync(
        UpdateOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        var currency = command.Currency?.Trim().ToUpperInvariant();
        if (command.ExpectedValueMinor is < 0 ||
            (command.ExpectedValueMinor.HasValue != !string.IsNullOrWhiteSpace(currency)))
        {
            throw new ArgumentException("Expected value requires non-negative money and currency.");
        }
        if (currency is not null)
        {
            await OpportunityCommandSupport.EnsureCodeAsync(
                store.DbContext, MasterDataCodes.Currencies.Collection, currency, cancellationToken);
        }
        return currency;
    }
}
