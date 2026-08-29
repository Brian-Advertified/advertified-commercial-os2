namespace Advertified.Commercial.Domain.Constants;

public static class Gate4Statuses
{
    public const string Created = "CREATED";
    public const string Qualifying = "QUALIFYING";
    public const string EvidenceReview = "EVIDENCE_REVIEW";
    public const string StrategyReady = "STRATEGY_READY";
    public const string BriefReady = "BRIEF_READY";
    public const string Planning = "PLANNING";
    public const string Pending = "PENDING";
    public const string Draft = "DRAFT";
    public const string InReview = "IN_REVIEW";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string WaitingForHuman = "WAITING_FOR_HUMAN";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string Active = "ACTIVE";
}

public static class Gate4RunKinds
{
    public const string Interpretation = "INTERPRETATION";
    public const string Angles = "ANGLES";
    public const string StrategyCritic = "STRATEGY_CRITIC";
    public const string Brief = "BRIEF";

    public static bool IsKnown(string value) =>
        value is Interpretation or Angles or StrategyCritic or Brief;
}

public static class Gate4TaskTypes
{
    public const string EvidenceItemReview = "EVIDENCE_ITEM_REVIEW";
    public const string EvidenceSetApproval = "EVIDENCE_SET_APPROVAL";
    public const string InterpretationConfirmation = "INTERPRETATION_CONFIRMATION";
    public const string AngleSelection = "ANGLE_SELECTION";
    public const string CriticResolution = "CRITIC_RESOLUTION";
    public const string StrategyApproval = "STRATEGY_APPROVAL";
    public const string RunRecovery = "RUN_RECOVERY";
    public const string BriefApproval = "BRIEF_APPROVAL";
}

public static class Gate4SourceTypes
{
    public const string SuppliedText = "SUPPLIED_TEXT";
    public const string PermittedUrl = "PERMITTED_URL";
    public const string DeterministicFixtureUrl =
        "https://fixtures.advertified.local/local-business";
}

public static class Gate4ReviewDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Edit = "EDIT";
}

public static class Gate4AngleStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Selected = "SELECTED";
    public const string Rejected = "REJECTED";
}

public static class Gate4CriticSeverities
{
    public const string Critical = "CRITICAL";
    public const string Material = "MATERIAL";
}

public static class Gate4ObjectionResolutions
{
    public const string Addressed = "ADDRESSED";
    public const string AcceptedWithReason = "ACCEPTED_WITH_REASON";
}

public static class Gate4EvidenceCodes
{
    public const string BusinessContext = "BUSINESS_CONTEXT";
    public const string OwnerSupplied = "OWNER_SUPPLIED";
}

public static class Gate4AgentCodes
{
    public const string BusinessInterpretation = "business_interpretation";
    public const string OpportunityIntelligence = "opportunity_intelligence";
    public const string Strategy = "strategy";
    public const string CriticReadiness = "critic_readiness";
    public const string BriefDrafting = "brief_drafting";
}

public static class Gate4StepCodes
{
    public const string Interpretation = "INTERPRETATION";
    public const string Angles = "ANGLES";
    public const string Strategy = "STRATEGY";
    public const string Critic = "CRITIC";
    public const string Brief = "BRIEF";
}

public static class Gate4ReviewerRoles
{
    public const string PlatformAdmin = "platform_admin";
    public const string AgencyAdmin = "agency_admin";
    public const string AgentRuntimeService = "agent_runtime_service";
    public static readonly string[] Evidence = ["platform_admin", "inventory_ops"];
    public static readonly string[] Strategy = ["platform_admin", "advertiser_approver"];
    public static readonly string[] Brief =
        ["internal_planner", "agency_admin", "agency_campaign_user"];
}

public static class Gate5BriefSourceTypes
{
    public const string SuppliedText = "SUPPLIED_TEXT";
    public const string Opportunity = "OPPORTUNITY";
}
