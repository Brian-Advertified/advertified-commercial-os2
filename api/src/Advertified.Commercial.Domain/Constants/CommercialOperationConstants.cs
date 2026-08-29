using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Constants;

public static class CommercialResourceTypes
{
    public static readonly ResourceTypeCode Tenant = new("tenant");
    public static readonly ResourceTypeCode User = new("user");
    public static readonly ResourceTypeCode ClientAccount = new("client_account");
    public static readonly ResourceTypeCode Agency = new("agency");
    public static readonly ResourceTypeCode Contact = new("contact");
}

public static class CommercialActions
{
    public static readonly ActionCode TenantUpdated = new("tenant.updated");
    public static readonly ActionCode UserUpdated = new("user.updated");
    public static readonly ActionCode ClientAccountCreated = new("client_account.created");
    public static readonly ActionCode AgencyCreated = new("agency.created");
    public static readonly ActionCode ContactCreated = new("contact.created");
}

public static class CommercialEventTypes
{
    public static readonly EventTypeCode TenantUpdated = new("TenantUpdated");
    public static readonly EventTypeCode UserUpdated = new("UserUpdated");
    public static readonly EventTypeCode ClientAccountCreated = new("ClientAccountCreated");
    public static readonly EventTypeCode AgencyCreated = new("AgencyCreated");
    public static readonly EventTypeCode ContactCreated = new("ContactCreated");
}
