using Advertified.Commercial.Application.Onboarding;

namespace Advertified.Commercial.Infrastructure.Onboarding;

internal sealed record PublicIntakeRow(
    Guid Id,
    string TypeCode,
    string Name,
    string Email,
    string? Phone,
    string Organisation,
    string? Website,
    string? Relationship,
    string? Message,
    string Status,
    DateTimeOffset CreatedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    Guid? ProvisionedTenantId,
    Guid? ProvisionedUserId,
    long Version)
{
    internal PublicIntakeView ToView() => new(
        Id, TypeCode, Name, Email, Phone, Organisation, Website,
        Relationship, Message, Status, CreatedAtUtc, ReviewedBy,
        ReviewedAtUtc, ReviewReason, ProvisionedTenantId,
        ProvisionedUserId, Version);
}
