namespace Advertified.Commercial.Domain.Lifecycle;

public enum OpportunityState
{
    Created = 1,
    Qualifying = 2,
    EvidenceReview = 3,
    StrategyReady = 4,
    BriefReady = 5,
    Planning = 6,
    ProposalReady = 7,
    Won = 8,
    Lost = 9,
    Archived = 10,
}

public enum OpportunityCommand
{
    StartQualification = 1,
    SubmitEvidenceForReview = 2,
    ApproveEvidenceSet = 3,
    ApproveStrategy = 4,
    ApproveBrief = 5,
    ApproveProposal = 6,
    MarkWon = 7,
    MarkLost = 8,
    Archive = 9,
}

public enum OpportunityEvent
{
    QualificationStarted = 1,
    EvidenceSubmitted = 2,
    EvidenceApproved = 3,
    StrategyApproved = 4,
    BriefApproved = 5,
    ProposalApproved = 6,
    Won = 7,
    Lost = 8,
    Archived = 9,
}

public sealed record OpportunityTransition(
    OpportunityState From,
    OpportunityCommand Command,
    OpportunityState To,
    OpportunityEvent Event);

public static class OpportunityStateMachine
{
    private static readonly OpportunityState[] LossEligibleStates =
    [
        OpportunityState.Created,
        OpportunityState.Qualifying,
        OpportunityState.EvidenceReview,
        OpportunityState.StrategyReady,
        OpportunityState.BriefReady,
        OpportunityState.Planning,
        OpportunityState.ProposalReady,
    ];

    private static readonly Dictionary<(OpportunityState, OpportunityCommand), OpportunityTransition>
        Transitions = BuildTransitions();

    public static OpportunityTransition Apply(OpportunityState current, OpportunityCommand command)
    {
        return Transitions.TryGetValue((current, command), out var transition)
            ? transition
            : throw new InvalidOperationException(
                $"Command {command} is not permitted from opportunity state {current}.");
    }

    private static Dictionary<(OpportunityState, OpportunityCommand), OpportunityTransition>
        BuildTransitions()
    {
        var transitions = new Dictionary<(OpportunityState, OpportunityCommand), OpportunityTransition>();

        Add(transitions, OpportunityState.Created, OpportunityCommand.StartQualification,
            OpportunityState.Qualifying, OpportunityEvent.QualificationStarted);
        Add(transitions, OpportunityState.Qualifying, OpportunityCommand.SubmitEvidenceForReview,
            OpportunityState.EvidenceReview, OpportunityEvent.EvidenceSubmitted);
        Add(transitions, OpportunityState.EvidenceReview, OpportunityCommand.ApproveEvidenceSet,
            OpportunityState.StrategyReady, OpportunityEvent.EvidenceApproved);
        Add(transitions, OpportunityState.StrategyReady, OpportunityCommand.ApproveStrategy,
            OpportunityState.BriefReady, OpportunityEvent.StrategyApproved);
        Add(transitions, OpportunityState.BriefReady, OpportunityCommand.ApproveBrief,
            OpportunityState.Planning, OpportunityEvent.BriefApproved);
        Add(transitions, OpportunityState.Planning, OpportunityCommand.ApproveProposal,
            OpportunityState.ProposalReady, OpportunityEvent.ProposalApproved);
        Add(transitions, OpportunityState.ProposalReady, OpportunityCommand.MarkWon,
            OpportunityState.Won, OpportunityEvent.Won);

        foreach (var state in LossEligibleStates)
        {
            Add(transitions, state, OpportunityCommand.MarkLost,
                OpportunityState.Lost, OpportunityEvent.Lost);
        }

        Add(transitions, OpportunityState.Won, OpportunityCommand.Archive,
            OpportunityState.Archived, OpportunityEvent.Archived);
        Add(transitions, OpportunityState.Lost, OpportunityCommand.Archive,
            OpportunityState.Archived, OpportunityEvent.Archived);

        return transitions;
    }

    private static void Add(
        Dictionary<(OpportunityState, OpportunityCommand), OpportunityTransition> transitions,
        OpportunityState from,
        OpportunityCommand command,
        OpportunityState to,
        OpportunityEvent @event)
    {
        transitions.Add((from, command), new OpportunityTransition(from, command, to, @event));
    }
}
