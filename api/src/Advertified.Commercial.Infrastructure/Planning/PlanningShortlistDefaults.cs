namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningShortlistDefaults
{
    internal static readonly string[] Assumptions =
    [
        "Hard eligibility is evaluated before governed suitability scoring.",
        "Inventory is planning-available unless an overlapping exception or confirmed booking conflict exists.",
        "Suitability uses the versioned OOH policy and sponsored placement never changes rank.",
    ];
}
