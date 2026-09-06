using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Proposal;

namespace Advertified.Commercial.Api.Tests;

internal sealed class ProposalNarrativeFixture(
    ProposalPolicy proposalPolicy) : IProposalNarrativeClient
{
    public Task<ProposalNarrative> CreateAsync(
        ProposalNarrativeInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Options.Count < proposalPolicy.MinimumOptions ||
            input.Options.Count > proposalPolicy.MaximumOptions)
        {
            throw new ArgumentException("The proposal choice count is outside the account policy.");
        }
        var optionSummary = string.Join(" ", input.Options.Select(option =>
            $"{option.Label} invests {ProposalMoneyFormatter.Format(option.BudgetMinor, option.Currency)} across " +
            $"{string.Join(", ", option.Channels)} to {LowerFirst(option.Outcome)}."));
        return Task.FromResult(new ProposalNarrative(
            $"The approved campaign objective is {LowerFirst(input.BriefObjective)}. {optionSummary}",
            0));
    }

    private static string LowerFirst(string value) => string.IsNullOrEmpty(value)
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];
}
