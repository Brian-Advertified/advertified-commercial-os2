using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Application.Inventory;

public sealed record IssueSupplierClaimInvitationCommand(
    string Email,
    string Role = MasterDataCodes.Roles.SupplierUser,
    int ValidForDays = 7);

public sealed record RevokeSupplierClaimInvitationCommand(string Reason);

public sealed record AcceptSupplierClaimInvitationCommand(string Token);

public sealed record ResolveProposalInventoryImpactCommand(
    Guid ReplacementProposalVersionId,
    string Resolution);

public sealed record SupplierClaimInvitationView(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string InvitedEmail,
    string Role,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string? RegistrationToken,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid? AcceptedUserId,
    DateTimeOffset? AcceptedAtUtc,
    long Version);

public sealed record InventorySupplierReleaseView(
    Guid Id,
    Guid SupplierId,
    Guid? SourceImportId,
    int VersionNumber,
    string ReplacementMode,
    string Status,
    Guid? SupersedesReleaseId,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset? SupersededAtUtc,
    int ProductCount,
    long Version);

public sealed record InventorySupplierLifecycleView(
    Guid Id,
    string Name,
    string ClaimStatus,
    Guid? CurrentReleaseId,
    int CurrentProductCount,
    int ExpiredProductCount,
    IReadOnlyList<InventorySupplierReleaseView> Releases,
    IReadOnlyList<SupplierClaimInvitationView> Invitations,
    long Version);

public sealed record ProposalInventoryImpactView(
    Guid Id,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid MediaPlanLineId,
    Guid InventoryTenantId,
    Guid SupplierId,
    Guid OldReleaseId,
    Guid ReplacementReleaseId,
    Guid OldProductId,
    Guid OldProductVersionId,
    Guid OldRateId,
    Guid? OldAvailabilityId,
    Guid? ReplacementProductId,
    Guid? ReplacementProductVersionId,
    Guid? ReplacementRateId,
    Guid? ReplacementAvailabilityId,
    string ImpactType,
    string Status,
    string ComparisonJson,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAtUtc,
    long Version);

public interface IInventorySupplierLifecycleCommands
{
    Task<CommandResult<SupplierClaimInvitationView>> IssueInvitationAsync(
        Guid supplierId,
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<SupplierClaimInvitationView>> RevokeInvitationAsync(
        Guid invitationId,
        CommandEnvelope<RevokeSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<SupplierClaimInvitationView>> AcceptInvitationAsync(
        Guid invitationId,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalInventoryImpactView>> ResolveProposalImpactAsync(
        Guid impactId,
        CommandEnvelope<ResolveProposalInventoryImpactCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IInventorySupplierLifecycleReader
{
    Task<InventorySupplierLifecycleView> GetSupplierAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProposalInventoryImpactView>> ListProposalImpactsAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken);
}

public sealed class SupplierIdentityAmbiguousException : Exception
{
    public SupplierIdentityAmbiguousException()
        : base("The supplier identity requires administrator review.") { }
}

public sealed class SupplierClaimInvitationInvalidException : Exception
{
    public SupplierClaimInvitationInvalidException()
        : base("The supplier registration invitation is no longer valid.") { }
}

public sealed class ProposalInventoryReviewRequiredException : Exception
{
    public ProposalInventoryReviewRequiredException()
        : base("The proposal uses inventory that has been replaced.") { }
}
