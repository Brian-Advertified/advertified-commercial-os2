using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingCommands
{
    private async Task<CommandOutcome> CreateOutcomeAsync(
        CommandEnvelope<CreateBookingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var source = await store.FindSelectedSourceAsync(
            envelope.TenantId, envelope.Command, now, cancellationToken)
            ?? throw new BookingReviewRequiredException();
        var policy = await policyStore.FindVersionAsync(
            envelope.TenantId, source.CommercialPolicyVersionId, cancellationToken)
            ?? throw new CommercialPolicyNotConfiguredException();
        var money = Calculate(policy, source);
        var terms = BookingPolicy.RequiredTerms(envelope.Command.Terms);
        var bookingId = Guid.NewGuid();
        var changed = await store.InsertAsync(
            bookingId, envelope, source, policy, money, terms, now, cancellationToken);
        if (changed != 1)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var view = await LoadViewAsync(bookingId, cancellationToken);
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.BookingCreated,
            MasterDataReferences.CommercialEventTypes.BookingCreated, now);
    }

    private static CommercialMoneyBreakdown Calculate(
        CommercialPolicyRow policy,
        BookingSourceRow source)
    {
        if (!string.Equals(policy.Currency, source.Currency, StringComparison.Ordinal))
        {
            throw new BookingReviewRequiredException();
        }
        var money = CommercialMoneyCalculator.Calculate(
            source.SupplierCostMinor, 0,
            new CommercialRatePolicy(
                policy.MarkupBasisPoints, policy.ManagementFeeBasisPoints,
                policy.CommissionBasisPoints, policy.VatStatus,
                policy.VatRateBasisPoints, policy.PricesIncludeVat));
        var fees = checked(
            money.MarkupMinor + money.CommissionMinor + money.ManagementFeeMinor);
        if (money.TotalMinor != source.ClientPriceMinor || fees != source.FeesMinor ||
            money.VatMinor != source.VatMinor)
        {
            throw new BookingReviewRequiredException();
        }
        return money;
    }
}
