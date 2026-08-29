namespace Advertified.Commercial.Application.Commands;

public sealed class IdempotencyConflictException()
    : Exception("The request key was already used for different information.");

public sealed class PreconditionRequiredException()
    : Exception("The current record version is required.");

public sealed class IdempotencyKeyRequiredException()
    : Exception("A request key is required.");
