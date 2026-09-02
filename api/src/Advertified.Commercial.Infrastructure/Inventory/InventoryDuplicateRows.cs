using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryDuplicateCandidateRow(
    Guid Id,
    Guid LeftProductId,
    Guid RightProductId,
    Guid LeftProductVersionId,
    Guid RightProductVersionId,
    string LeftName,
    string RightName,
    string Method,
    decimal? Similarity,
    string EvidenceJson,
    string Status,
    Guid? CanonicalProductId,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version)
{
    internal InventoryDuplicateCandidateView ToView() => new(
        Id, LeftProductId, RightProductId, LeftProductVersionId,
        RightProductVersionId, LeftName, RightName, Method, Similarity,
        EvidenceJson, Status, CanonicalProductId, ReviewedBy,
        ReviewedAtUtc, ReviewReason, Version);
}

internal sealed record InventorySemanticRecallRow(
    Guid ProductId,
    Guid ProductVersionId,
    string Name,
    string Geography,
    decimal Similarity)
{
    internal InventorySemanticRecallView ToView() => new(
        ProductId, ProductVersionId, Name, Geography, Similarity);
}
