using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private const string AmbiguousDeliveryMessage =
        "The email provider may have accepted this proposal. Check or reconcile the existing delivery; do not resend it.";
    private const string AcceptedDeliveryRecordingMessage =
        "The email provider accepted this proposal. Complete the existing local delivery record; do not resend it.";
    private const string PreDeliveryFailureMessage =
        "The proposal automation stopped before delivery. Retry after checking the service.";
    private const string ConfirmedDeliveryFailureMessage =
        "The email provider rejected the delivery request. Review the provider response before any new send.";

    private async Task<EmailAutomationRunView> RecordReviewRequiredAsync(
        TenantId tenantId,
        ActorId owner,
        Guid inboundEmailId,
        EmailAutomationReviewRequiredException exception,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var run = await SetFailureStateAsync(
            tenantId, owner, inboundEmailId,
            MasterDataCodes.EmailAutomationStatuses.ReviewRequired,
            exception.FailureCode, exception.Message, correlationId, cancellationToken);
        return EmailAutomationRecordStore.ToView(run);
    }

    private async Task<EmailAutomationRunView> RecordUnexpectedFailureAsync(
        TenantId tenantId,
        EmailAutomationContextRow context,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var run = await SetFailureStateAsync(
            tenantId, owner, context.InboundEmailId,
            MasterDataCodes.EmailAutomationStatuses.Failed,
            MasterDataCodes.AutomationFailureReasons.DeliveryFailed,
            PreDeliveryFailureMessage,
            correlationId, cancellationToken);
        return EmailAutomationRecordStore.ToView(run);
    }

    private async Task<EmailAutomationRunView> RecordConfirmedDeliveryFailureAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var run = await SetFailureStateAsync(
            tenantId, actorId, inboundEmailId,
            MasterDataCodes.EmailAutomationStatuses.Failed,
            MasterDataCodes.AutomationFailureReasons.DeliveryFailed,
            ConfirmedDeliveryFailureMessage,
            correlationId, cancellationToken, deliveryRejected: true);
        return EmailAutomationRecordStore.ToView(run);
    }

    private Task<EmailAutomationRunRow> SetFailureStateAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        string status,
        string failureCode,
        string message,
        CorrelationId correlationId,
        CancellationToken cancellationToken,
        bool deliveryRejected = false) =>
        store.UpdateRunWithTransitionAsync(
            tenantId,
            actorId,
            inboundEmailId,
            current => ClassifyFailure(
                current, status, failureCode, message, deliveryRejected),
            desired => CreateFailureTransition(desired, correlationId),
            cancellationToken);

    private EmailAutomationRunRow ClassifyFailure(
        EmailAutomationRunRow current,
        string status,
        string failureCode,
        string message,
        bool deliveryRejected)
    {
        if (current.Status == MasterDataCodes.EmailAutomationStatuses.Sent)
        {
            return current;
        }
        if (current.DeliveryAcceptedAtUtc.HasValue)
        {
            return WithFailure(
                current,
                MasterDataCodes.EmailAutomationStatuses.ReviewRequired,
                MasterDataCodes.AutomationFailureReasons.DeliveryRecordingRequired,
                AcceptedDeliveryRecordingMessage);
        }
        if (current.DeliveryRequestedAtUtc.HasValue && !deliveryRejected)
        {
            return WithFailure(
                current,
                MasterDataCodes.EmailAutomationStatuses.ReviewRequired,
                MasterDataCodes.AutomationFailureReasons.DeliveryAmbiguous,
                AmbiguousDeliveryMessage);
        }
        return WithFailure(current, status, failureCode, message);
    }

    private EmailAutomationRunRow WithFailure(
        EmailAutomationRunRow current,
        string status,
        string failureCode,
        string message) =>
        current with
        {
            Status = status,
            FailureCode = failureCode,
            FailureMessage = message,
            UpdatedAtUtc = timeProvider.GetUtcNow(),
        };

    private static EmailAutomationTransition CreateFailureTransition(
        EmailAutomationRunRow desired,
        CorrelationId correlationId)
    {
        var reviewRequired = desired.Status ==
            MasterDataCodes.EmailAutomationStatuses.ReviewRequired;
        return new EmailAutomationTransition(
            new CommandId(Guid.NewGuid()),
            correlationId,
            reviewRequired
                ? MasterDataReferences.CommercialActions.EmailAutomationReviewRequired
                : MasterDataReferences.CommercialActions.EmailAutomationFailed,
            reviewRequired
                ? MasterDataReferences.CommercialEventTypes.EmailProposalAutomationReviewRequired
                : MasterDataReferences.CommercialEventTypes.EmailProposalAutomationFailed);
    }
}
