using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Constants;

public static class MasterDataConstants
{
    public static readonly LifecycleStatusCode ActiveStatus = new("ACTIVE");

    public const string RoleCollection = "roles";
    public const string PermissionCollection = "permissions";
    public const string ContactPurposeCollection = "contactPurposes";
}

public static class Gate2Permissions
{
    public static readonly PermissionCode WorkspaceRead = new("workspace_read");
    public static readonly PermissionCode TenantRead = new("tenant_read");
    public static readonly PermissionCode TenantManage = new("tenant_manage");
    public static readonly PermissionCode UserReadSelf = new("user_read_self");
    public static readonly PermissionCode UserManageSelf = new("user_manage_self");
    public static readonly PermissionCode MembershipRead = new("membership_read");
    public static readonly PermissionCode MembershipManage = new("membership_manage");
    public static readonly PermissionCode ClientAccountRead = new("client_account_read");
    public static readonly PermissionCode ClientAccountManage = new("client_account_manage");
    public static readonly PermissionCode AgencyRead = new("agency_read");
    public static readonly PermissionCode AgencyManage = new("agency_manage");
    public static readonly PermissionCode ContactRead = new("contact_read");
    public static readonly PermissionCode ContactManage = new("contact_manage");
}

public static class Gate4Permissions
{
    public static readonly PermissionCode OpportunityView = new("opportunity_view");
    public static readonly PermissionCode OpportunityCreate = new("opportunity_create");
    public static readonly PermissionCode OpportunityEdit = new("opportunity_edit");
    public static readonly PermissionCode EvidenceCreate = new("evidence_create");
    public static readonly PermissionCode EvidenceReview = new("evidence_review");
    public static readonly PermissionCode AgentRun = new("agent_run");
    public static readonly PermissionCode AngleSelect = new("opportunity_angle_select");
    public static readonly PermissionCode StrategyView = new("strategy_view");
    public static readonly PermissionCode StrategyApprove = new("strategy_approve");
    public static readonly PermissionCode RunView = new("run_view");
    public static readonly PermissionCode RunManage = new("run_manage");
    public static readonly PermissionCode TaskView = new("task_view");
    public static readonly PermissionCode TaskAct = new("task_act");
}

public static class Gate5Permissions
{
    public static readonly PermissionCode BriefView = new("brief_view");
    public static readonly PermissionCode BriefCreate = new("brief_create");
    public static readonly PermissionCode BriefEdit = new("brief_edit");
    public static readonly PermissionCode BriefSubmit = new("brief_submit");
    public static readonly PermissionCode BriefApprove = new("brief_approve");
}

public static class Gate6Permissions
{
    public static readonly PermissionCode InventoryView = new("inventory_view");
    public static readonly PermissionCode InventoryImport = new("inventory_import");
    public static readonly PermissionCode InventoryReview = new("inventory_review");
    public static readonly PermissionCode InventoryPublish = new("inventory_publish");
}
