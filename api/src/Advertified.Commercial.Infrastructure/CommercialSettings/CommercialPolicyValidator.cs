using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

internal static class CommercialPolicyValidator
{
    private static readonly HashSet<string> ActiveCurrencies = LoadCodes(
        MasterDataCodes.Currencies.Collection);
    private static readonly HashSet<string> ActiveVatStatuses = LoadCodes(
        MasterDataCodes.VatStatuses.Collection);

    internal static ValidatedCommercialPolicy Validate(SaveCommercialPolicyCommand command)
    {
        var currency = command.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var vatStatus = command.VatStatus?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ActiveCurrencies.Contains(currency) || !ActiveVatStatuses.Contains(vatStatus) ||
            command.BookingApprovalThresholdMinor < 0)
        {
            throw new ArgumentException("The commercial policy uses an unsupported value.");
        }
        var policy = new CommercialRatePolicy(
            command.MarkupBasisPoints,
            command.ManagementFeeBasisPoints,
            command.CommissionBasisPoints,
            vatStatus,
            command.VatRateBasisPoints,
            command.PricesIncludeVat);
        _ = CommercialMoneyCalculator.Calculate(0, 0, policy);
        return new ValidatedCommercialPolicy(
            command.MarkupBasisPoints,
            command.ManagementFeeBasisPoints,
            command.CommissionBasisPoints,
            vatStatus,
            command.VatRateBasisPoints,
            command.PricesIncludeVat,
            currency,
            command.BookingApprovalThresholdMinor);
    }

    private static HashSet<string> LoadCodes(string collection) =>
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == collection).Items
            .Where(item => item.IsActive)
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
}
