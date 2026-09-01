using Advertified.Commercial.Application.CommercialSettings;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

internal sealed record CommercialPolicyRow(
    Guid Id,
    Guid PolicyId,
    Guid TenantId,
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
    long Version)
{
    internal CommercialPolicyView ToView() => new(
        Id,
        PolicyId,
        VersionNumber,
        MarkupBasisPoints,
        ManagementFeeBasisPoints,
        CommissionBasisPoints,
        VatStatus,
        VatRateBasisPoints,
        PricesIncludeVat,
        Currency,
        BookingApprovalThresholdMinor,
        AllowSelfApproval,
        CreatedBy,
        CreatedAtUtc,
        Version);
}

internal sealed record ValidatedCommercialPolicy(
    int MarkupBasisPoints,
    int ManagementFeeBasisPoints,
    int CommissionBasisPoints,
    string VatStatus,
    int VatRateBasisPoints,
    bool PricesIncludeVat,
    string Currency,
    long BookingApprovalThresholdMinor,
    bool AllowSelfApproval);
