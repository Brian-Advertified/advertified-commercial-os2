using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Domain.Constants;

public static class OpportunityReviewerRoles
{
    public static readonly string[] Evidence =
        [MasterDataCodes.Roles.PlatformAdmin, MasterDataCodes.Roles.InventoryOps];

    public static readonly string[] Strategy =
        [MasterDataCodes.Roles.PlatformAdmin, MasterDataCodes.Roles.AdvertiserApprover];

    public static readonly string[] Brief =
    [
        MasterDataCodes.Roles.InternalPlanner,
        MasterDataCodes.Roles.AgencyAdmin,
        MasterDataCodes.Roles.AgencyCampaignUser,
    ];
}

public static class InventoryReviewerRoles
{
    public static readonly string[] Inventory =
        [MasterDataCodes.Roles.InventoryOps, MasterDataCodes.Roles.PlatformAdmin];
}

public static class OpportunityFixtureUris
{
    public const string LocalBusiness =
        "https://fixtures.advertified.local/local-business";
}
