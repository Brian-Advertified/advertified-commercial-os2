using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Application.Opportunity;

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
                "EVIDENCE_REQUIRED"),
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
