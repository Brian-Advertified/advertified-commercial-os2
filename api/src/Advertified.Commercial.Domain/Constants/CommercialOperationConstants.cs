using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Constants;

public static class CommercialResourceTypes
{
    public static readonly ResourceTypeCode Tenant = new("tenant");
    public static readonly ResourceTypeCode User = new("user");
    public static readonly ResourceTypeCode ClientAccount = new("client_account");
    public static readonly ResourceTypeCode Agency = new("agency");
    public static readonly ResourceTypeCode Contact = new("contact");
    public static readonly ResourceTypeCode Opportunity = new("opportunity");
    public static readonly ResourceTypeCode EvidenceSource = new("evidence_source");
    public static readonly ResourceTypeCode EvidenceItem = new("evidence_item");
    public static readonly ResourceTypeCode EvidenceSet = new("evidence_set");
    public static readonly ResourceTypeCode Interpretation = new("business_interpretation");
    public static readonly ResourceTypeCode OpportunityAngle = new("opportunity_angle");
    public static readonly ResourceTypeCode Strategy = new("strategy");
    public static readonly ResourceTypeCode AgentRun = new("agent_run");
    public static readonly ResourceTypeCode HumanTask = new("human_task");
    public static readonly ResourceTypeCode CampaignBrief = new("campaign_brief");
    public static readonly ResourceTypeCode BriefVersion = new("brief_version");
}

public static class CommercialActions
{
    public static readonly ActionCode TenantUpdated = new("tenant.updated");
    public static readonly ActionCode UserUpdated = new("user.updated");
    public static readonly ActionCode ClientAccountCreated = new("client_account.created");
    public static readonly ActionCode AgencyCreated = new("agency.created");
    public static readonly ActionCode ContactCreated = new("contact.created");
    public static readonly ActionCode OpportunityCreated = new("opportunity.created");
    public static readonly ActionCode OpportunityUpdated = new("opportunity.updated");
    public static readonly ActionCode OpportunityQualificationStarted =
        new("opportunity.qualification_started");
    public static readonly ActionCode EvidenceRegistered = new("evidence.registered");
    public static readonly ActionCode EvidenceReviewed = new("evidence.reviewed");
    public static readonly ActionCode EvidenceSubmitted = new("evidence.submitted");
    public static readonly ActionCode EvidenceApproved = new("evidence.approved");
    public static readonly ActionCode AgentRunQueued = new("agent_run.queued");
    public static readonly ActionCode InterpretationConfirmed = new("interpretation.confirmed");
    public static readonly ActionCode AngleSelected = new("opportunity_angle.selected");
    public static readonly ActionCode ObjectionResolved = new("critic_objection.resolved");
    public static readonly ActionCode StrategySubmitted = new("strategy.submitted");
    public static readonly ActionCode StrategyApproved = new("strategy.approved");
    public static readonly ActionCode StrategyRejected = new("strategy.rejected");
    public static readonly ActionCode AgentRunResumed = new("agent_run.resumed");
    public static readonly ActionCode AgentRunCancelled = new("agent_run.cancelled");
    public static readonly ActionCode BriefCreated = new("campaign_brief.created");
    public static readonly ActionCode BriefVersionCreated = new("brief_version.created");
    public static readonly ActionCode BriefSubmitted = new("brief_version.submitted");
    public static readonly ActionCode BriefApproved = new("brief_version.approved");
    public static readonly ActionCode BriefRejected = new("brief_version.rejected");
}

public static class CommercialEventTypes
{
    public static readonly EventTypeCode TenantUpdated = new("TenantUpdated");
    public static readonly EventTypeCode UserUpdated = new("UserUpdated");
    public static readonly EventTypeCode ClientAccountCreated = new("ClientAccountCreated");
    public static readonly EventTypeCode AgencyCreated = new("AgencyCreated");
    public static readonly EventTypeCode ContactCreated = new("ContactCreated");
    public static readonly EventTypeCode OpportunityCreated = new("OpportunityCreated");
    public static readonly EventTypeCode OpportunityUpdated = new("OpportunityUpdated");
    public static readonly EventTypeCode OpportunityQualificationStarted =
        new("OpportunityQualificationStarted");
    public static readonly EventTypeCode EvidenceRegistered = new("EvidenceRegistered");
    public static readonly EventTypeCode EvidenceReviewed = new("EvidenceReviewed");
    public static readonly EventTypeCode OpportunityEvidenceSubmitted = new("OpportunityEvidenceSubmitted");
    public static readonly EventTypeCode OpportunityEvidenceApproved = new("OpportunityEvidenceApproved");
    public static readonly EventTypeCode AgentRunQueued = new("AgentRunQueued");
    public static readonly EventTypeCode InterpretationConfirmed = new("InterpretationConfirmed");
    public static readonly EventTypeCode OpportunityAngleSelected = new("OpportunityAngleSelected");
    public static readonly EventTypeCode CriticObjectionResolved = new("CriticObjectionResolved");
    public static readonly EventTypeCode StrategySubmitted = new("StrategySubmitted");
    public static readonly EventTypeCode StrategyApproved = new("StrategyApproved");
    public static readonly EventTypeCode StrategyRejected = new("StrategyRejected");
    public static readonly EventTypeCode AgentRunResumed = new("AgentRunResumed");
    public static readonly EventTypeCode AgentRunCancelled = new("AgentRunCancelled");
    public static readonly EventTypeCode CampaignBriefCreated = new("CampaignBriefCreated");
    public static readonly EventTypeCode BriefVersionCreated = new("BriefVersionCreated");
    public static readonly EventTypeCode BriefSubmitted = new("BriefSubmitted");
    public static readonly EventTypeCode BriefApproved = new("BriefApproved");
    public static readonly EventTypeCode BriefRejected = new("BriefRejected");
}
