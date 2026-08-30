using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;

namespace Advertified.Commercial.Application.Planning;

public sealed record SelectCampaignModeCommand(
    string Mode,
    string DecisionSource,
    decimal Confidence,
    string? Reason);

public sealed record CampaignModeSelectionView(
    Guid Id,
    Guid BriefVersionId,
    string Mode,
    IReadOnlyList<string> AllowedChannels,
    bool IsLocked,
    string DecisionSource,
    decimal Confidence,
    string? Reason,
    Guid SelectedBy,
    DateTimeOffset SelectedAtUtc);

public sealed class CampaignModeRequiredException : Exception
{
    public CampaignModeRequiredException()
        : base("Choose out-of-home only or full campaign before planning begins.")
    {
    }
}

public sealed class CampaignModeLockedException : Exception
{
    public CampaignModeLockedException()
        : base("The campaign mode is locked. Start a new campaign to choose another mode.")
    {
    }
}
