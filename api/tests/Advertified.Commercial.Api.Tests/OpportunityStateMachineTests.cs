using Advertified.Commercial.Domain.Lifecycle;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OpportunityStateMachineTests
{
    [Theory]
    [InlineData(
        OpportunityState.Created,
        OpportunityCommand.StartQualification,
        OpportunityState.Qualifying,
        OpportunityEvent.QualificationStarted)]
    [InlineData(
        OpportunityState.Qualifying,
        OpportunityCommand.SubmitEvidenceForReview,
        OpportunityState.EvidenceReview,
        OpportunityEvent.EvidenceSubmitted)]
    [InlineData(
        OpportunityState.EvidenceReview,
        OpportunityCommand.ApproveEvidenceSet,
        OpportunityState.StrategyReady,
        OpportunityEvent.EvidenceApproved)]
    [InlineData(
        OpportunityState.StrategyReady,
        OpportunityCommand.ApproveStrategy,
        OpportunityState.BriefReady,
        OpportunityEvent.StrategyApproved)]
    [InlineData(
        OpportunityState.BriefReady,
        OpportunityCommand.ApproveBrief,
        OpportunityState.Planning,
        OpportunityEvent.BriefApproved)]
    [InlineData(
        OpportunityState.Planning,
        OpportunityCommand.ApproveProposal,
        OpportunityState.ProposalReady,
        OpportunityEvent.ProposalApproved)]
    [InlineData(
        OpportunityState.ProposalReady,
        OpportunityCommand.MarkWon,
        OpportunityState.Won,
        OpportunityEvent.Won)]
    public void CanonicalTransitionResolves(
        OpportunityState from,
        OpportunityCommand command,
        OpportunityState to,
        OpportunityEvent expectedEvent)
    {
        var transition = OpportunityStateMachine.Apply(from, command);

        Assert.Equal(to, transition.To);
        Assert.Equal(expectedEvent, transition.Event);
    }

    [Fact]
    public void MarkLostIsAvailableOnlyBeforeADecision()
    {
        var openStates = Enum.GetValues<OpportunityState>()
            .Where(state => state <= OpportunityState.ProposalReady);

        foreach (var state in openStates)
        {
            var transition = OpportunityStateMachine.Apply(
                state,
                OpportunityCommand.MarkLost);

            Assert.Equal(OpportunityState.Lost, transition.To);
        }

        Assert.Throws<InvalidOperationException>(() =>
            OpportunityStateMachine.Apply(
                OpportunityState.Won,
                OpportunityCommand.MarkLost));
    }

    [Fact]
    public void InvalidTransitionIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OpportunityStateMachine.Apply(
                OpportunityState.Created,
                OpportunityCommand.ApproveBrief));
    }
}
