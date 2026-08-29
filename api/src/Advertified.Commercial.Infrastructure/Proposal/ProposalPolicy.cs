using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed record ProposalPolicy(
    string Version,
    int MinimumOptions,
    int MaximumOptions,
    int DefaultValidityDays,
    int MaximumValidityDays)
{
    public static ProposalPolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var collection = registry.Collections.SingleOrDefault(item =>
            item.Code == MasterDataCodes.ProposalPolicies.Collection);
        var item = collection?.Items.SingleOrDefault(value =>
            value.Code == MasterDataCodes.ProposalPolicies.ClientOptionsV1 && value.IsActive)
            ?? throw new InvalidOperationException(
                "The canonical proposal policy is missing from master data.");
        using var metadata = JsonDocument.Parse(item.MetadataJson);
        var root = metadata.RootElement;
        var minimum = root.GetProperty("minimumOptions").GetInt32();
        var maximum = root.GetProperty("maximumOptions").GetInt32();
        var defaultValidityDays = root.GetProperty("defaultValidityDays").GetInt32();
        var validityDays = root.GetProperty("maximumValidityDays").GetInt32();
        if (minimum < 1 || maximum < minimum || defaultValidityDays < 1 ||
            validityDays < defaultValidityDays)
        {
            throw new InvalidOperationException("The canonical proposal policy is invalid.");
        }
        return new ProposalPolicy(
            item.Code, minimum, maximum, defaultValidityDays, validityDays);
    }
}
