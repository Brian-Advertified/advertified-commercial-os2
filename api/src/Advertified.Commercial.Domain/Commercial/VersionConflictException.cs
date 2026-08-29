namespace Advertified.Commercial.Domain.Commercial;

public sealed class VersionConflictException()
    : Exception("The record was changed by another operation.");
