namespace Advertified.Commercial.Application.Opportunity;

public sealed class EvidenceRequiredException()
    : Exception("Approved evidence is required before this action.");

public sealed class ApprovalRequiredException()
    : Exception("A different assigned human approval is required.");

public sealed class RunNotResumableException()
    : Exception("The workflow run cannot be resumed from its current state.");

public sealed class CaptureProviderDisabledException()
    : Exception("Live source capture is disabled.");

public sealed class InvalidLifecycleTransitionException()
    : Exception("The requested lifecycle transition is not valid.");
