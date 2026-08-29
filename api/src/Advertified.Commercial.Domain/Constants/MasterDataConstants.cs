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
