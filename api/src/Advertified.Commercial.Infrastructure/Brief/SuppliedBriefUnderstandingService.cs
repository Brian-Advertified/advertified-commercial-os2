using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class SuppliedBriefUnderstandingService(
    ISuppliedBriefAgentClient agentClient,
    SuppliedBriefAgentPolicy policy,
    ITenantAuthorizer authorizer) : ISuppliedBriefUnderstandingService
{
    public async Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        ActorId actorId,
        TenantId tenantId,
        UnderstandSuppliedBriefRequest request,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.BriefCreate,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Brief access denied.");
        }
        var title = Required(request.SourceTitle, 300, nameof(request.SourceTitle));
        var content = Required(request.SourceContent, 262_144, nameof(request.SourceContent));
        var clarifications = (request.Clarifications ?? Array.Empty<BriefClarificationInput>())
            .Select(ValidateClarification)
            .ToArray();
        var result = await agentClient.UnderstandAsync(new SuppliedBriefAgentInput(
            tenantId.Value,
            actorId.Value,
            title,
            content,
            clarifications), cancellationToken);
        ValidateResult(result);
        return result;
    }

    private void ValidateResult(SuppliedBriefUnderstandingView result)
    {
        if (result.Usage.IncrementalCostMinor < 0 || result.Usage.ToolCalls < 0 ||
            result.CampaignModeConfidence is < 0 or > 1 ||
            result.Evidence.Any(item => item.Confidence is < 0 or > 1) ||
            result.Questions.Select(item => item.FieldPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Questions.Count)
        {
            throw new InvalidOperationException("The Brief-understanding result is invalid.");
        }
        if (result.CampaignMode is not null &&
            result.CampaignMode is not (MasterDataCodes.CampaignModes.OohOnly or
                MasterDataCodes.CampaignModes.FullCampaign))
        {
            throw new InvalidOperationException("The Brief-understanding campaign mode is invalid.");
        }
        var requiresChoice = result.CampaignMode is null ||
            result.CampaignModeConfidence < policy.MinimumModeConfidence;
        if (requiresChoice != result.Questions.Any(item =>
                string.Equals(item.FieldPath, "campaignMode", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "The Brief-understanding campaign-mode clarification is inconsistent.");
        }
    }

    private static BriefClarificationInput ValidateClarification(BriefClarificationInput input) =>
        new(
            Required(input.FieldPath, 200, nameof(input.FieldPath)),
            Required(input.Value, 4000, nameof(input.Value)));

    private static string Required(string value, int maximumLength, string parameterName)
    {
        var result = value.Trim();
        if (result.Length == 0 || result.Length > maximumLength)
        {
            throw new ArgumentException("A valid Brief value is required.", parameterName);
        }
        return result;
    }
}
