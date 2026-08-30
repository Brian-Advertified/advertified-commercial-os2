using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed record EmailAutomationPolicy(
    string Version,
    string CampaignMode,
    IReadOnlyList<string> AllowedChannels,
    IReadOnlyList<string> RequiredBriefFields,
    decimal MinimumStpConfidence,
    string MinimumSupplyConfidence,
    int MaximumProposalOptions,
    long MaximumSourceBytes,
    int MaximumClarificationCount,
    int MaximumClarificationLength,
    int MaximumRetryReasonLength,
    bool RequiresTenantOptIn,
    bool AllowAutomaticExternalSend,
    bool AllowAttachments,
    string ProposalTitle,
    string ProposalOptionLabel,
    string ProposalTerms,
    string EmailSubjectPrefix,
    string EmailBody)
{
    public static EmailAutomationPolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var item = registry.Collections.Single(collection =>
                collection.Code == MasterDataCodes.EmailAutomationPolicies.Collection).Items
            .Single(value => value.Code ==
                MasterDataCodes.EmailAutomationPolicies.OohInboundProposalV1 && value.IsActive);
        using var document = JsonDocument.Parse(item.MetadataJson);
        var root = document.RootElement;
        var policy = new EmailAutomationPolicy(
            item.Code,
            root.GetProperty("campaignMode").GetString()!,
            ReadStrings(root, "allowedChannels"),
            ReadStrings(root, "requiredBriefFields"),
            root.GetProperty("minimumStpConfidence").GetDecimal(),
            root.GetProperty("minimumSupplyConfidence").GetString()!,
            root.GetProperty("maximumProposalOptions").GetInt32(),
            root.GetProperty("maximumSourceBytes").GetInt64(),
            root.GetProperty("maximumClarificationCount").GetInt32(),
            root.GetProperty("maximumClarificationLength").GetInt32(),
            root.GetProperty("maximumRetryReasonLength").GetInt32(),
            root.GetProperty("requiresTenantOptIn").GetBoolean(),
            root.GetProperty("allowAutomaticExternalSend").GetBoolean(),
            root.GetProperty("allowAttachments").GetBoolean(),
            ReadText(root, "proposalTitle"),
            ReadText(root, "proposalOptionLabel"),
            ReadText(root, "proposalTerms"),
            ReadText(root, "emailSubjectPrefix"),
            ReadText(root, "emailBody"));
        Validate(policy);
        return policy;
    }

    private static string[] ReadStrings(JsonElement root, string name) =>
        root.GetProperty(name).EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string ReadText(JsonElement root, string name)
    {
        var value = root.GetProperty(name).GetString()?.Trim() ?? string.Empty;
        return value.Length > 0
            ? value
            : throw new InvalidOperationException(
                "The inbound OOH automation content policy is invalid.");
    }

    private static void Validate(EmailAutomationPolicy policy)
    {
        if (policy.CampaignMode != MasterDataCodes.CampaignModes.OohOnly ||
            policy.AllowedChannels.Count == 0 ||
            policy.AllowedChannels.Any(channel => channel is not
                (MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh)) ||
            policy.RequiredBriefFields.Count == 0 ||
            policy.MinimumStpConfidence is < 0 or > 1 ||
            policy.MaximumProposalOptions != 1 ||
            policy.MaximumSourceBytes <= 0 ||
            policy.MaximumClarificationCount <= 0 ||
            policy.MaximumClarificationLength <= 0 ||
            policy.MaximumRetryReasonLength <= 0 ||
            !policy.RequiresTenantOptIn ||
            !policy.AllowAutomaticExternalSend)
        {
            throw new InvalidOperationException("The inbound OOH automation policy is invalid.");
        }
    }
}
