using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed class DeterministicProposalNarrativeClient(
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
            $"{option.Label} invests {FormatMoney(option.BudgetMinor, option.Currency)} across " +
            $"{string.Join(", ", option.Channels)} to {LowerFirst(option.Outcome)}."));
        return Task.FromResult(new ProposalNarrative(
            $"The approved campaign objective is {LowerFirst(input.BriefObjective)}. {optionSummary}",
            0));
    }

    private static string FormatMoney(long amountMinor, string currency) =>
        $"{currency} {(decimal)amountMinor / 100m:N0}";

    private static string LowerFirst(string value) => string.IsNullOrEmpty(value)
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];
}
