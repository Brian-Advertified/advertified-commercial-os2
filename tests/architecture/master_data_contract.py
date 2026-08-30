"""Expected security-sensitive shape of canonical master data."""

REQUIRED_MASTER_COLLECTIONS = {
    "assetTypes",
    "channels",
    "contactPurposes",
    "currencies",
    "lifecycleStatuses",
    "paymentMethods",
    "permissions",
    "opportunitySourceTypes",
    "evidenceSourceTypes",
    "evidencePolicyBases",
    "evidenceClaimTypes",
    "evidenceReviewDecisions",
    "opportunityAngleStatuses",
    "criticSeverities",
    "objectionResolutions",
    "humanTaskTypes",
    "briefSourceTypes",
    "documentClasses",
    "inventoryReviewDecisions",
    "availabilityStatuses",
    "verificationLevels",
    "inventoryProductTypes",
    "inventoryImportStepTypes",
    "malwareScanStatuses",
    "proposalDeliveryModes",
    "proposalTiers",
    "rateTypes",
    "rejectionReasons",
    "roles",
    "taskPriorities",
    "tenantTypes",
    "vatStatuses",
    "agentRunKinds",
    "agentTypes",
    "workflowStepTypes",
    "inventoryTransformationTypes",
    "inventoryUnsupportedClaimTerms",
    "planningObjectionTypes",
    "benchmarkExclusionReasons",
    "benchmarkPositions",
    "evidenceClassifications",
    "planningPolicies",
    "proposalPolicies",
    "campaignModes",
    "campaignModeDecisionSources",
    "briefUnderstandingPolicies",
    "emailProviders",
    "emailAutomationStatuses",
    "emailAutomationCheckpoints",
    "automationFailureReasons",
    "emailAutomationPolicies",
    "marketplaceListingStatuses",
    "marketplaceRfqStatuses",
    "marketplaceResponseStatuses",
    "rateFreshnessStatuses",
    "supplyConfidenceStatuses",
    "supplySourceTypes",
    "validationIssueTypes",
    "agentFailureReasons",
    "creativeTextRoles",
    "creativeWarningTypes",
    "assetRightsStatuses",
    "commercialResourceTypes",
    "commercialActions",
    "commercialEventTypes",
}

BASIC_HUMAN_ROLES = {
    "platform_admin",
    "internal_planner",
    "inventory_ops",
    "agency_admin",
    "agency_campaign_user",
    "advertiser_admin",
    "advertiser_approver",
    "supplier_admin",
    "supplier_user",
    "influencer_rep",
}
ADMIN_ROLES = {
    "platform_admin",
    "agency_admin",
    "advertiser_admin",
    "supplier_admin",
}
AGENCY_ADMIN_ROLES = {"platform_admin", "agency_admin"}
FOUNDATION_PERMISSION_ROLES = {
    "workspace_read": BASIC_HUMAN_ROLES,
    "tenant_read": BASIC_HUMAN_ROLES,
    "tenant_manage": ADMIN_ROLES,
    "user_read_self": BASIC_HUMAN_ROLES,
    "user_manage_self": BASIC_HUMAN_ROLES,
    "membership_read": ADMIN_ROLES,
    "membership_manage": ADMIN_ROLES,
    "client_account_read": AGENCY_ADMIN_ROLES,
    "client_account_manage": AGENCY_ADMIN_ROLES,
    "agency_read": AGENCY_ADMIN_ROLES | {"agency_campaign_user"},
    "agency_manage": AGENCY_ADMIN_ROLES,
    "contact_read": AGENCY_ADMIN_ROLES,
    "contact_manage": AGENCY_ADMIN_ROLES,
}
OPPORTUNITY_PERMISSION_ROLES = {
    "opportunity_view": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "advertiser_admin", "advertiser_approver",
    },
    "opportunity_create": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "opportunity_edit": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "evidence_create": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "evidence_review": {"platform_admin", "inventory_ops"},
    "agent_run": {"platform_admin", "internal_planner"},
    "opportunity_angle_select": {"platform_admin", "internal_planner"},
    "strategy_view": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "advertiser_admin", "advertiser_approver",
    },
    "strategy_approve": {"platform_admin", "advertiser_approver"},
    "run_view": {"platform_admin", "internal_planner"},
    "run_manage": {"platform_admin", "internal_planner"},
    "task_view": BASIC_HUMAN_ROLES,
    "task_act": BASIC_HUMAN_ROLES,
}
BRIEF_PERMISSION_ROLES = {
    "brief_view": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "advertiser_admin", "advertiser_approver",
    },
    "brief_create": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "brief_edit": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "brief_submit": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "brief_approve": {"internal_planner", "agency_admin", "agency_campaign_user"},
}
INVENTORY_PERMISSION_ROLES = {
    "inventory_view": {
        "platform_admin", "internal_planner", "inventory_ops", "agency_admin",
        "agency_campaign_user", "advertiser_admin", "advertiser_approver",
        "supplier_admin", "supplier_user",
    },
    "inventory_import": {"platform_admin", "inventory_ops", "supplier_admin"},
    "inventory_review": {"platform_admin", "inventory_ops"},
    "inventory_publish": {"platform_admin", "inventory_ops"},
}
PLANNING_PERMISSION_ROLES = {
    "plan_view": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "advertiser_admin", "advertiser_approver", "worker_service",
    },
    "plan_generate": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "worker_service",
    },
    "plan_edit": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "worker_service",
    },
    "plan_approve": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "worker_service",
    },
}
PROPOSAL_PERMISSION_ROLES = {
    "proposal_view": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
        "advertiser_admin", "advertiser_approver",
    },
    "proposal_generate": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "proposal_edit": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "proposal_approve": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "proposal_share": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "proposal_decide": {"advertiser_admin", "advertiser_approver"},
}
EMAIL_AUTOMATION_PERMISSION_ROLES = {
    "email_automation_view": {"platform_admin", "internal_planner", "agency_admin"},
    "email_automation_manage": {"platform_admin", "agency_admin"},
    "email_automation_execute": {"platform_admin", "agency_admin", "worker_service"},
}
MARKETPLACE_PERMISSION_ROLES = {
    "marketplace_view": BASIC_HUMAN_ROLES - {"influencer_rep"},
    "supplier_inventory_manage": {"platform_admin", "supplier_admin", "supplier_user"},
    "rfq_create": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "rfq_send": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
    "rfq_respond": {"platform_admin", "supplier_admin", "supplier_user"},
    "rfq_review": {
        "platform_admin", "internal_planner", "agency_admin", "agency_campaign_user",
    },
}
REQUIRED_PERMISSION_ROLES = (
    FOUNDATION_PERMISSION_ROLES | OPPORTUNITY_PERMISSION_ROLES | BRIEF_PERMISSION_ROLES
    | INVENTORY_PERMISSION_ROLES | PLANNING_PERMISSION_ROLES | PROPOSAL_PERMISSION_ROLES
    | EMAIL_AUTOMATION_PERMISSION_ROLES | MARKETPLACE_PERMISSION_ROLES
)

