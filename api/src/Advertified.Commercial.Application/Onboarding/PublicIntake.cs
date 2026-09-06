using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Onboarding;

public sealed record SubmitPublicIntakeRequest(
    string TypeCode,
    string Name,
    string Email,
    string? Phone,
    string Organisation,
    string? Website,
    string? Relationship,
    string? Message);

public sealed record ProvisionPublicIntakeCommand(
    string LegalName,
    string TradingName,
    string? Website,
    string? VatNumber,
    bool RequireMfa,
    string Reason);

public sealed record RejectPublicIntakeCommand(string Reason);

public sealed record ResolvePublicIntakeCommand(string Reason);

public sealed record PublicIntakeView(
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
    long Version);

public interface IPublicIntakeReader
{
    Task<CursorPage<PublicIntakeView>> ListAsync(
        ActorId actorId,
        TenantId platformTenantId,
        string? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);
}

public interface IPublicIntakeCommands
{
    Task<PublicIntakeView> SubmitAsync(
        SubmitPublicIntakeRequest request,
        CancellationToken cancellationToken);

    Task<CommandResult<PublicIntakeView>> ProvisionAsync(
        Guid requestId,
        CommandEnvelope<ProvisionPublicIntakeCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PublicIntakeView>> RejectAsync(
        Guid requestId,
        CommandEnvelope<RejectPublicIntakeCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PublicIntakeView>> ResolveAsync(
        Guid requestId,
        CommandEnvelope<ResolvePublicIntakeCommand> envelope,
        CancellationToken cancellationToken);
}
