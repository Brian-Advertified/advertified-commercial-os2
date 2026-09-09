using System.Net;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task<(Guid ShortlistId, Guid CandidateId)> CreateOlderSelectionDraftAsync(HttpClient client)
    {
        using var draft = await CommandAsync(client,
            Path($"brief-versions/{BriefVersionId}/shortlists:generate"), "decision-older-draft", 1, new { });
        var candidate = draft.RootElement.GetProperty("candidates").EnumerateArray()
            .First(item => item.GetProperty("isEligible").GetBoolean());
        return (draft.RootElement.GetProperty("id").GetGuid(), candidate.GetProperty("id").GetGuid());
    }

    private static async Task AssertOlderSelectionDraftRejectedAsync(HttpClient client,
        (Guid ShortlistId, Guid CandidateId) draft)
    {
        using var response = await RawCommandAsync(client, Path($"shortlist-versions/{draft.ShortlistId}:select"),
            "decision-out-of-order", 1, new
            {
                selectedCandidateIds = new[] { draft.CandidateId },
                reason = "An old draft must not supersede the newer confirmed choice.",
            });
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "PLANNING_INPUT_STALE");
    }
}
