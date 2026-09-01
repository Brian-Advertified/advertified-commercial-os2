using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.CommercialSettings;

public sealed record SaveCommercialPolicyCommand(
    int MarkupBasisPoints,
    int ManagementFeeBasisPoints,
    int CommissionBasisPoints,
    string VatStatus,
    int VatRateBasisPoints,
    bool PricesIncludeVat,
    string Currency,
    long BookingApprovalThresholdMinor,
    bool AllowSelfApproval);

public sealed record CommercialPolicyView(
    Guid Id,
    Guid PolicyId,
    int VersionNumber,
    int MarkupBasisPoints,
    int ManagementFeeBasisPoints,
    int CommissionBasisPoints,
    string VatStatus,
    int VatRateBasisPoints,
    bool PricesIncludeVat,
    string Currency,
    long BookingApprovalThresholdMinor,
    bool AllowSelfApproval,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    long Version);

public interface ICommercialPolicyCommands
{
    Task<CommandResult<CommercialPolicyView>> SaveAsync(
        CommandEnvelope<SaveCommercialPolicyCommand> envelope,
        CancellationToken cancellationToken);
}

public interface ICommercialPolicyReader
{
    Task<CommercialPolicyView> GetCurrentAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);
}

public sealed class CommercialPolicyNotConfiguredException : Exception;
