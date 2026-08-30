using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed record SuppliedBriefAgentPolicy(
    string Version,
    IReadOnlyDictionary<string, string[]> ChannelTerms,
    string[] OohTerms,
    string[] FullCampaignTerms,
    string[] ClientLabels,
    string[] ProblemLabels,
    string[] ObjectiveLabels,
    string[] AudienceLabels,
    string[] GeographyLabels,
    string[] TimingLabels,
    string[] MediaLabels,
    string[] MeasurementLabels,
    string[] ConstraintLabels,
    string DefaultCurrency,
    decimal MinimumModeConfidence)
{
    private const string BriefTermsField = "briefTerms";
    private const string DetectionTermsField = "detectionTerms";

    public static SuppliedBriefAgentPolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var modes = registry.Collections.Single(collection =>
            collection.Code == MasterDataCodes.CampaignModes.Collection);
        var ooh = modes.Items.Single(item =>
            item.Code == MasterDataCodes.CampaignModes.OohOnly && item.IsActive);
        var full = modes.Items.Single(item =>
            item.Code == MasterDataCodes.CampaignModes.FullCampaign && item.IsActive);
        var policyItem = registry.Collections.Single(collection =>
                collection.Code == MasterDataCodes.BriefUnderstandingPolicies.Collection)
            .Items.Single(item =>
                item.Code == MasterDataCodes.BriefUnderstandingPolicies.BriefUnderstandingV1 &&
                item.IsActive);
        var channels = registry.Collections.Single(collection =>
                collection.Code == MasterDataCodes.Channels.Collection)
            .Items.Where(item => item.IsActive)
            .ToDictionary(
                item => item.Code,
                item => ReadStringArray(item.MetadataJson, BriefTermsField),
                StringComparer.Ordinal);

        using var metadata = JsonDocument.Parse(policyItem.MetadataJson);
        var root = metadata.RootElement;
        var result = new SuppliedBriefAgentPolicy(
            policyItem.Code,
            channels,
            ReadStringArray(ooh.MetadataJson, DetectionTermsField),
            ReadStringArray(full.MetadataJson, DetectionTermsField),
            ReadStringArray(root, "clientLabels"),
            ReadStringArray(root, "problemLabels"),
            ReadStringArray(root, "objectiveLabels"),
            ReadStringArray(root, "audienceLabels"),
            ReadStringArray(root, "geographyLabels"),
            ReadStringArray(root, "timingLabels"),
            ReadStringArray(root, "mediaLabels"),
            ReadStringArray(root, "measurementLabels"),
            ReadStringArray(root, "constraintLabels"),
            root.GetProperty("defaultCurrency").GetString()
                ?? throw new InvalidOperationException("The default Brief currency is missing."),
            root.GetProperty("minimumRouteConfidence").GetDecimal());
        Validate(result);
        return result;
    }

    private static string[] ReadStringArray(string metadataJson, string property)
    {
        using var metadata = JsonDocument.Parse(metadataJson);
        return ReadStringArray(metadata.RootElement, property);
    }

    private static string[] ReadStringArray(JsonElement root, string property) =>
        root.TryGetProperty(property, out var values)
            ? values.EnumerateArray()
                .Select(item => item.GetString()?.Trim().ToLowerInvariant())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

    private static void Validate(SuppliedBriefAgentPolicy policy)
    {
        if (policy.ChannelTerms.Count == 0 || policy.OohTerms.Length == 0 ||
            policy.FullCampaignTerms.Length == 0 || policy.ClientLabels.Length == 0 ||
            policy.ObjectiveLabels.Length == 0 || policy.AudienceLabels.Length == 0 ||
            policy.GeographyLabels.Length == 0 || policy.TimingLabels.Length == 0 ||
            policy.MinimumModeConfidence is <= 0 or > 1 ||
            string.IsNullOrWhiteSpace(policy.DefaultCurrency))
        {
            throw new InvalidOperationException(
                "The governed Brief-understanding policy is incomplete.");
        }
    }
}
