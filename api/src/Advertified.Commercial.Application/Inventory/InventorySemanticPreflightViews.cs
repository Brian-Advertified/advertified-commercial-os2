using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public interface IInventorySemanticPreflightReader
{
    Task<InventorySemanticPreflightView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid? importId,
        CancellationToken cancellationToken);
}

public sealed record InventorySemanticSourcePreflightView(
    Guid ImportId,
    Guid InputArtifactId,
    string FileName,
    string SourceHash,
    string DocumentClass,
    string ImportStatus,
    bool SafeToReproject,
    int PacketCount,
    int ImageCount,
    int SourceItemCount,
    long MaximumCostUsdMicros,
    long NewMaximumCostUsdMicros,
    long LargestPacketCostUsdMicros,
    string? Blocker);

public sealed record InventorySemanticPreflightView(
    string ProjectionVersion,
    string Provider,
    string Model,
    string PromptVersion,
    string BudgetScope,
    long InputPricePerMillionTokensUsdMicros,
    long OutputPricePerMillionTokensUsdMicros,
    long PerCallCostCapUsdMicros,
    long CertificationBudgetUsdMicros,
    long ExistingCommittedCostUsdMicros,
    long NewMaximumCostUsdMicros,
    long WorstCaseTotalCostUsdMicros,
    bool LiveExecutionEnabled,
    bool ReadyToActivate,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<InventorySemanticSourcePreflightView> Sources);
