using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Errors;

public sealed class HumanSafeExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<HumanSafeExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1001, nameof(HumanSafeExceptionHandler)),
            "Commercial API request failed. CorrelationId: {CorrelationId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogRequestFailure(logger, httpContext.TraceIdentifier, exception);

        var safe = Map(exception);
        httpContext.Response.StatusCode = safe.Status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new HumanSafeProblemDetails
            {
                Status = safe.Status,
                Title = safe.Title,
                Detail = safe.Detail,
                Type = $"https://advertified.local/problems/{safe.Code.ToLowerInvariant()}",
                Code = safe.Code,
                CorrelationId = httpContext.TraceIdentifier,
            },
        });
    }

    private static SafeProblem Map(Exception exception)
    {
        return exception switch
        {
            BrowserAntiforgeryException => new(
                StatusCodes.Status403Forbidden,
                "Request could not be verified",
                "Refresh the page and try again.",
                "CSRF_VALIDATION_FAILED"),
            BrowserOriginException => new(
                StatusCodes.Status403Forbidden,
                "Request origin not allowed",
                "Open Advertified from its configured local address and try again.",
                "ORIGIN_NOT_ALLOWED"),
            UnauthorizedAccessException => new(
                StatusCodes.Status403Forbidden,
                "Access denied",
                "You do not have access to this workspace or action.",
                "TENANT_FORBIDDEN"),
            CommercialPolicyNotConfiguredException => new(
                StatusCodes.Status404NotFound,
                "Commercial policy is not configured",
                "Ask an agency or platform administrator to configure this workspace policy.",
                "COMMERCIAL_POLICY_NOT_CONFIGURED"),
            BookingReviewRequiredException => new(
                StatusCodes.Status409Conflict,
                "Booking needs review",
                "The selected price, policy, rate, or availability changed. Create a new plan and client-confirmed proposal before booking.",
                "BOOKING_REVIEW_REQUIRED"),
            CampaignReadinessBlockedException => new(
                StatusCodes.Status409Conflict,
                "Campaign is not ready",
                "Complete confirmed funding and every required booking before advancing the campaign.",
                "CAMPAIGN_READINESS_BLOCKED"),
            FundingReviewRequiredException => new(
                StatusCodes.Status409Conflict,
                "Funding needs review",
                "The selected option, amount, currency, or funding evidence no longer reconciles.",
                "FUNDING_REVIEW_REQUIRED"),
            PaymentMethodUnavailableException => new(
                StatusCodes.Status409Conflict,
                "Payment method is unavailable",
                "Use manual EFT locally. Provider and credit methods require separate approval and configuration.",
                "PAYMENT_METHOD_UNAVAILABLE"),
            VersionConflictException or DbUpdateConcurrencyException => new(
                StatusCodes.Status409Conflict,
                "Changes could not be saved",
                "This information changed while you were working. Refresh and try again.",
                "VERSION_CONFLICT"),
            IdempotencyConflictException => new(
                StatusCodes.Status409Conflict,
                "Request key already used",
                "Use a new request key for different information.",
                "IDEMPOTENCY_CONFLICT"),
            PreconditionRequiredException => new(
                StatusCodes.Status428PreconditionRequired,
                "Current version required",
                "Refresh this information and try again.",
                "PRECONDITION_REQUIRED"),
            IdempotencyKeyRequiredException => new(
                StatusCodes.Status400BadRequest,
                "Request key required",
                "Add a request key and try again.",
                "IDEMPOTENCY_KEY_REQUIRED"),
            EvidenceRequiredException => new(
                StatusCodes.Status409Conflict,
                "Approved evidence required",
                "Review and approve the required evidence before continuing.",
                MasterDataCodes.AgentFailureReasons.EvidenceRequired),
            ApprovalRequiredException => new(
                StatusCodes.Status403Forbidden,
                "Assigned approval required",
                "A different assigned reviewer or approver must complete this action.",
                "APPROVAL_REQUIRED"),
            RunNotResumableException => new(
                StatusCodes.Status409Conflict,
                "Run cannot be resumed",
                "Create a corrected run or review the recorded recovery action.",
                "RUN_NOT_RESUMABLE"),
            CaptureProviderDisabledException => new(
                StatusCodes.Status409Conflict,
                "Source capture is unavailable",
                "Supply approved source text or use an available deterministic fixture.",
                "CAPTURE_PROVIDER_DISABLED"),
            InvalidLifecycleTransitionException => new(
                StatusCodes.Status409Conflict,
                "Action is not available",
                "Refresh this opportunity and complete its current step first.",
                "INVALID_LIFECYCLE_TRANSITION"),
            InventoryPublishBlockedException => new(
                StatusCodes.Status409Conflict,
                "Inventory is not ready to publish",
                "Resolve the blocking candidate fields and complete review before publishing.",
                "INVENTORY_PUBLISH_BLOCKED"),
            InventoryProtectionUnavailableException or FileNotFoundException => new(
                StatusCodes.Status503ServiceUnavailable,
                "File protection is unavailable",
                "Try again after the local file protection services are available.",
                "INVENTORY_PROTECTION_UNAVAILABLE"),
            UnsafeInventorySourceException => new(
                StatusCodes.Status409Conflict,
                "File was rejected",
                "Use a supported clean file and try again.",
                "UNSAFE_FILE_REJECTED"),
            CampaignModeRequiredException => new(
                StatusCodes.Status409Conflict,
                "Choose the campaign mode",
                "Select out-of-home only or a full campaign before continuing.",
                "CAMPAIGN_MODE_REQUIRED"),
            CampaignModeLockedException => new(
                StatusCodes.Status409Conflict,
                "The campaign mode is already set",
                "Start a new campaign and planning process to use another mode.",
                "CAMPAIGN_MODE_LOCKED"),
            SupplyConfirmationRequiredException => new(
                StatusCodes.Status409Conflict,
                "Supplier confirmation required",
                "Confirm the current rate and availability for the selected placement before continuing.",
                MasterDataCodes.RejectionReasons.SupplierConfirmationRequired),
            CampaignRestartRequiredException => new(
                StatusCodes.Status409Conflict,
                "Start a new campaign",
                "This OOH-only campaign cannot add another media type. Create a new campaign and begin again from the Brief.",
                "CAMPAIGN_RESTART_REQUIRED"),
            PlanningInputStaleException => new(
                StatusCodes.Status409Conflict,
                "Planning inputs changed",
                "Regenerate the affected shortlist or plan from current inventory truth.",
                "PLANNING_INPUT_STALE"),
            PlanningApprovalBlockedException => new(
                StatusCodes.Status409Conflict,
                "Planning approval is blocked",
                "Resolve material objections and reconcile totals before approval.",
                "PLANNING_APPROVAL_BLOCKED"),
            InventoryBenchmarkUnavailableException => new(
                StatusCodes.Status409Conflict,
                "Market comparison is unavailable",
                "This product does not have a current comparable OOH rate yet.",
                "INVENTORY_BENCHMARK_UNAVAILABLE"),
            MarketplaceListingUnavailableException => new(
                StatusCodes.Status409Conflict,
                "Listing is not currently available",
                "Ask the supplier to publish current rate and availability details.",
                "MARKETPLACE_LISTING_UNAVAILABLE"),
            MarketplaceResponseExpiredException => new(
                StatusCodes.Status409Conflict,
                "Supplier response expired",
                "Ask the supplier for a current response before accepting it.",
                "MARKETPLACE_RESPONSE_EXPIRED"),
            ProposalStaleException => new(
                StatusCodes.Status409Conflict,
                "Proposal inputs changed",
                "Create a new proposal from the current approved media plans.",
                "PROPOSAL_STALE"),
            ProposalDocumentRequiredException => new(
                StatusCodes.Status409Conflict,
                "Proposal document required",
                "Approve and render the current proposal before sharing it with the client.",
                "PROPOSAL_DOCUMENT_REQUIRED"),
            ProposalExpiredException => new(
                StatusCodes.Status409Conflict,
                "Proposal expired",
                "Ask the agency for a current proposal before making a decision.",
                "PROPOSAL_EXPIRED"),
            InvalidEmailWebhookException => new(
                StatusCodes.Status400BadRequest,
                "Inbound email notification rejected",
                "The email provider notification could not be verified.",
                "INVALID_EMAIL_WEBHOOK"),
            InboundMailboxNotConfiguredException => new(
                StatusCodes.Status409Conflict,
                "Inbound proposal mailbox is not ready",
                "Configure and enable the proposal mailbox before receiving requests.",
                "INBOUND_MAILBOX_NOT_CONFIGURED"),
            EmailAutomationReviewRequiredException review => new(
                StatusCodes.Status409Conflict,
                "This request needs review",
                review.Message,
                review.FailureCode),
            EmailAutomationNotRetryableException => new(
                StatusCodes.Status409Conflict,
                "This request cannot be retried",
                "Only a failed or review-required request can be retried.",
                "EMAIL_AUTOMATION_NOT_RETRYABLE"),
            EmailAttachmentBlockedException => new(
                StatusCodes.Status409Conflict,
                "An attachment needs review",
                "Review the supplied attachments before continuing this request.",
                "EMAIL_ATTACHMENT_BLOCKED"),
            EmailPayloadUnavailableException => new(
                StatusCodes.Status409Conflict,
                "Email content is unavailable",
                "Retrieve or resubmit the complete email before processing it.",
                "EMAIL_PAYLOAD_UNAVAILABLE"),
            EmailProviderUnavailableException or EmailDeliveryFailedException => new(
                StatusCodes.Status503ServiceUnavailable,
                "Email service is unavailable",
                "Retry after the configured email service is available.",
                "EMAIL_PROVIDER_UNAVAILABLE"),
            ArgumentException or BadHttpRequestException => new(
                StatusCodes.Status400BadRequest,
                "Some information needs attention",
                "Review the highlighted information and try again.",
                "VALIDATION_FAILED"),
            _ => new(
                StatusCodes.Status500InternalServerError,
                "Something went wrong",
                "Try again. If the problem continues, share the support reference.",
                "UNEXPECTED_ERROR"),
        };
    }

    private sealed record SafeProblem(int Status, string Title, string Detail, string Code);
}
