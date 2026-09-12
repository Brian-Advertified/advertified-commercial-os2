using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class SuppliedBriefUnderstandingService(
    ISuppliedBriefAgentClient agentClient,
    ITenantAuthorizer authorizer,
    ISuppliedBriefInterpretationStore interpretations) : ISuppliedBriefUnderstandingService
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
        _ = Required(request.SourceContent, 262_144, nameof(request.SourceContent));
        var content = request.SourceContent;
        if (content.Length > 262_144) throw new ArgumentException("The supplied Brief is too large.");
        var clarifications = (request.Clarifications ?? Array.Empty<BriefClarificationInput>())
            .Select(ValidateClarification)
            .ToArray();
        if (clarifications.Length > 100 || clarifications.Select(item => item.FieldPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != clarifications.Length)
            throw new ArgumentException("Brief corrections must use distinct bounded fields.");
        var input = new SuppliedBriefAgentInput(tenantId.Value, actorId.Value, title, content, clarifications);
        var reservation = await interpretations.ReserveAsync(input,
            request.InterpretationId ?? Guid.NewGuid(), request.ParentInterpretationId, cancellationToken);
        if (reservation.Retained is not null) return reservation.Retained;
        return await InterpretRetainedAsync(input with { Interpretation = reservation.Reference }, cancellationToken);
    }

    private async Task<SuppliedBriefUnderstandingView> InterpretRetainedAsync(
        SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        SuppliedBriefUnderstandingView result;
        try
        {
            result = await agentClient.UnderstandAsync(input, cancellationToken);
            try { ValidateResult(result); }
            catch (InvalidOperationException failure)
            {
                throw new SuppliedBriefValidationException(result.Usage,
                    System.Text.Json.JsonSerializer.Serialize(result), failure);
            }
        }
        catch (SuppliedBriefValidationException failure)
        {
            using var retention = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await interpretations.RejectAsync(input, failure, retention.Token);
            throw;
        }
        catch
        {
            using var retention = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await interpretations.FailAsync(input, retention.Token);
            throw;
        }
        // Retain accepted usage even when the caller disconnected after receiving provider output.
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        result = result with { Interpretation = input.Interpretation };
        await interpretations.CompleteAsync(input, result, completion.Token);
        return result;
    }

    private static void ValidateResult(SuppliedBriefUnderstandingView result)
    {
        if (result.Usage.IncrementalCostMinor < 0 || result.Usage.ToolCalls < 0 ||
            result.CampaignModeConfidence is < 0 or > 1 ||
            result.Evidence.Any(item => item.Confidence is < 0 or > 1) ||
            result.Questions.Select(item => item.FieldPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Questions.Count ||
            result.Questions.Any(item => !SuppliedBriefFieldPaths.IsSupported(item.FieldPath)) ||
            result.Evidence.Any(item => !SuppliedBriefFieldPaths.IsSupported(item.FieldPath)) ||
            result.Draft.Unknowns.Any(item => !SuppliedBriefFieldPaths.IsSupported(item.FieldPath)) ||
            result.Draft.Assumptions.Any(item => !SuppliedBriefFieldPaths.IsSupported(item.FieldPath)) ||
            result.Draft.Conflicts.Any(item => !SuppliedBriefFieldPaths.IsSupported(item.FieldPath)))
        {
            throw new InvalidOperationException("The Brief-understanding result is invalid.");
        }
        if (result.CampaignMode is not null &&
            result.CampaignMode is not (MasterDataCodes.CampaignModes.OohOnly or
                MasterDataCodes.CampaignModes.FullCampaign))
        {
            throw new InvalidOperationException("The Brief-understanding campaign mode is invalid.");
        }
        // Confidence is diagnostic metadata, never the campaign-mode decision rule.
        var requiresChoice = result.CampaignMode is null;
        if (requiresChoice != result.Questions.Any(item =>
                string.Equals(item.FieldPath, SuppliedBriefFieldPaths.CampaignMode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "The Brief-understanding campaign-mode clarification is inconsistent.");
        }
    }

    private static BriefClarificationInput ValidateClarification(BriefClarificationInput input)
    {
        var fieldPath = Required(input.FieldPath, 200, nameof(input.FieldPath));
        if (!SuppliedBriefFieldPaths.IsSupported(fieldPath))
        {
            throw new ArgumentException(
                "The Brief correction targets an unsupported field.",
                nameof(input));
        }
        return new(fieldPath, Required(input.Value, 4000, nameof(input.Value)));
    }

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
