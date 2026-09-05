using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventorySupplierLifecycleRow(
    Guid Id,
    string Name,
    string ClaimStatus,
    Guid? CurrentReleaseId,
    int CurrentProductCount,
    int ExpiredProductCount,
    long Version);

internal sealed record SupplierClaimInvitationRow(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string InvitedEmail,
    string Role,
    string TokenHash,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid? RevokedBy,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason,
    Guid? AcceptedUserId,
    DateTimeOffset? AcceptedAtUtc,
    long Version)
{
    internal SupplierClaimInvitationView ToView(string? registrationToken = null) => new(
        Id, SupplierId, SupplierName, InvitedEmail, Role, Status, ExpiresAtUtc,
        registrationToken, CreatedBy, CreatedAtUtc, AcceptedUserId, AcceptedAtUtc, Version);
}

internal sealed record SupplierIdentityMatchRow(
    Guid Id,
    string Name,
    string ClaimStatus,
    long Version);

internal sealed record TenantMembershipRoleRow(
    Guid Id,
    string Role,
    string Status,
    long Version);

internal sealed record InventorySupplierReleaseRow(
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
    long Version)
{
    internal InventorySupplierReleaseView ToView() => new(
        Id, SupplierId, SourceImportId, VersionNumber, ReplacementMode, Status,
        SupersedesReleaseId, EffectiveAtUtc, SupersededAtUtc, ProductCount, Version);
}

internal sealed record ProposalInventoryImpactRow(
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
    long Version)
{
    internal ProposalInventoryImpactView ToView() => new(
        Id, ProposalVersionId, ProposalOptionId, MediaPlanLineId, InventoryTenantId,
        SupplierId, OldReleaseId, ReplacementReleaseId, OldProductId,
        OldProductVersionId, OldRateId, OldAvailabilityId, ReplacementProductId,
        ReplacementProductVersionId, ReplacementRateId, ReplacementAvailabilityId,
        ImpactType, Status, ComparisonJson, ResolvedBy, ResolvedAtUtc, Version);
}
