namespace Advertified.Commercial.Domain.Constants;

public static class Gate6DocumentClasses
{
    public const string Csv = "CSV";
    public const string Xlsx = "XLSX";
    public const string Pdf = "PDF";
    public const string Docx = "DOCX";
    public const string Png = "PNG";
    public const string Jpeg = "JPEG";
}

public static class Gate6InventoryStatuses
{
    public const string Uploaded = "UPLOADED";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Active = "ACTIVE";
}

public static class Gate6ScanStatuses
{
    public const string Pending = "PENDING";
    public const string Clean = "CLEAN";
    public const string Infected = "INFECTED";
    public const string Error = "ERROR";
}

public static class Gate6ReviewDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Edit = "EDIT";
}

public static class Gate6Channels
{
    public const string Ooh = "OOH";
    public const string Dooh = "DOOH";
    public const string Radio = "RADIO";
    public const string Television = "TV";
    public const string Print = "PRINT";
    public const string Digital = "DIGITAL";
    public const string Social = "SOCIAL";
    public const string Influencer = "INFLUENCER";
    public const string Experiential = "EXPERIENTIAL";
    public const string Podcast = "PODCAST";
    public const string Retail = "RETAIL";
    public const string Transit = "TRANSIT";
    public const string Mall = "MALL";
    public const string Email = "EMAIL";
    public const string Mobile = "MOBILE";
}

public static class Gate6ProductTypes
{
    public const string OohSite = "OOH_SITE";
    public const string DoohScreen = "DOOH_SCREEN";
    public const string RadioSpot = "RADIO_SPOT";
    public const string TelevisionSpot = "TV_SPOT";
    public const string PrintPlacement = "PRINT_PLACEMENT";
    public const string DigitalPlacement = "DIGITAL_PLACEMENT";
    public const string SocialPlacement = "SOCIAL_PLACEMENT";
    public const string InfluencerPackage = "INFLUENCER_PACKAGE";
    public const string Experience = "EXPERIENCE";
    public const string PodcastSpot = "PODCAST_SPOT";
    public const string RetailPlacement = "RETAIL_PLACEMENT";
    public const string TransitPlacement = "TRANSIT_PLACEMENT";
    public const string MallPlacement = "MALL_PLACEMENT";
    public const string EmailPlacement = "EMAIL_PLACEMENT";
    public const string MobilePlacement = "MOBILE_PLACEMENT";
}

public static class Gate6Availability
{
    public const string Unknown = "UNKNOWN";
}

public static class Gate6Verification
{
    public const string HumanVerified = "HUMAN_VERIFIED";
}

public static class Gate6InventorySteps
{
    public const string Protection = "UPLOAD_PROTECTION";
    public const string Classification = "CLASSIFICATION";
    public const string Extraction = "EXTRACTION";
    public const string Normalization = "NORMALIZATION";
    public const string Validation = "VALIDATION";
    public const string Review = "REVIEW";
    public const string Publication = "PUBLICATION";
}

public static class Gate6TaskTypes
{
    public const string CandidateReview = "INVENTORY_CANDIDATE_REVIEW";
}

public static class Gate6TaskStatuses
{
    public const string Pending = "PENDING";
}

public static class Gate6ReviewerRoles
{
    public static readonly string[] Inventory = ["inventory_ops", "platform_admin"];
}

public static class Gate6Transformations
{
    public const string Trim = "TRIM";
    public const string UppercaseCode = "UPPERCASE_CODE";
    public const string MajorToMinor = "MAJOR_TO_MINOR";
    public const string ParseDecimal = "PARSE_DECIMAL";
    public const string DerivedFromChannel = "DERIVED_FROM_CHANNEL";
    public const string ExplicitUnknown = "EXPLICIT_UNKNOWN";
}
