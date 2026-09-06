using System.Security.Cryptography;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed record InventoryProjectionVerificationView(
    string FileName,
    string SourceHash,
    string DocumentClass,
    string ProjectionVersion,
    string AdapterCode,
    string AdapterVersion,
    string SchemaVersion,
    string ProviderJson,
    string ProviderOutputHash,
    string CanonicalOutputHash,
    int ExtractedRowCount,
    IReadOnlyList<InventoryProjectionCandidateView> ProjectedCandidates,
    InventorySourceAccountingReport SourceAccounting,
    IReadOnlyList<InventoryProjectionVerificationStageView> Stages);

public sealed record InventoryProjectionVerificationStageView(
    string Stage,
    string State,
    string Reason);

internal static class InventoryProjectionVerificationStages
{
    internal const string AdapterExtraction = "ADAPTER_EXTRACTION";
    internal const string DocumentProjection = "DOCUMENT_PROJECTION";
    internal const string SchemaExtraction = "SCHEMA_EXTRACTION";
    internal const string SemanticEnrichment = "SEMANTIC_ENRICHMENT";
    internal const string CandidatePreparation = "CANDIDATE_PREPARATION";
    internal const string BaseValidation = "BASE_VALIDATION";
    internal const string AcceptanceQuarantine = "ACCEPTANCE_QUARANTINE";
    internal const string Persistence = "PERSISTENCE";
    internal const string SourceAccounting = "SOURCE_ACCOUNTING";
    internal const string Publication = "PUBLICATION_STAGE";
}

public sealed class InventoryProjectionVerificationService(
    InventoryRecordStore store,
    ITenantAuthorizer authorizer,
    IInventoryDocumentExtractionAdapter adapter,
    IOptions<InventoryProcessingOptions> processingOptions,
    IOptions<InventoryProtectionOptions> protectionOptions,
    IOptions<InventorySemanticOptions> semanticOptions)
{
    public async Task<InventoryProjectionVerificationView> VerifyAsync(
        ActorId actorId,
        TenantId tenantId,
        InventorySourceFile source,
        CancellationToken cancellationToken)
    {
        EnsureSafeExecutionBoundary();
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        var document = InventoryDocumentClassifier.Detect(
            source.FileName,
            source.DeclaredMediaType,
            source.Content,
            protectionOptions.Value.MaximumSourceBytes);
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(source.Content)).ToLowerInvariant();
        var request = new InventoryExtractionRequest(
            source.FileName,
            document.MediaType,
            document.Code,
            sourceHash,
            source.Content);
        var extraction = await adapter.ExtractAsync(
            request, cancellationToken);
        if (!string.Equals(
                extraction.SourceHash,
                sourceHash,
                StringComparison.Ordinal))
        {
            throw new InventoryProtectionUnavailableException();
        }
        var codes = await InventoryCodeSets.LoadAsync(
            store.DbContext, cancellationToken);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
                extraction.Rows,
                sourceHash,
                string.Empty,
                codes,
                DateTimeOffset.UnixEpoch)
            .ToArray();
        extraction = InventoryExtractionSourceAccounting.Attach(
            extraction, candidates);
        var views = candidates.Select(candidate => new InventoryProjectionCandidateView(
                candidate.RowNumber,
                candidate.SourceLocator,
                candidate.Values,
                candidate.Evidence))
            .ToArray();
        return new(
            source.FileName,
            sourceHash,
            document.Code,
            InventoryProjectionVersion.Current(semanticOptions.Value),
            extraction.AdapterCode,
            extraction.AdapterVersion,
            extraction.SchemaVersion,
            extraction.ProviderJson,
            extraction.ProviderOutputHash,
            extraction.CanonicalOutputHash,
            extraction.Rows.Count,
            views,
            extraction.Document.SourceAccounting ??
                throw new InvalidOperationException("Source accounting was not produced."),
            VerificationStages());
    }

    internal static InventoryProjectionVerificationStageView[]
        VerificationStages() =>
    [
        Mapped(InventoryProjectionVerificationStages.AdapterExtraction,
            "The adapter result and exact provider JSON are retained."),
        Mapped(InventoryProjectionVerificationStages.DocumentProjection,
            "The authenticated Python Docling projection executed."),
        Mapped(InventoryProjectionVerificationStages.SchemaExtraction,
            "The versioned Python projector mapped source structures to typed rows."),
        Skipped(InventoryProjectionVerificationStages.SemanticEnrichment,
            "The read-only verifier does not execute semantic enrichment."),
        Mapped(InventoryProjectionVerificationStages.CandidatePreparation,
            "Candidate normalization and preparation executed."),
        Mapped(InventoryProjectionVerificationStages.BaseValidation,
            "Base candidate validation executed during preparation."),
        Skipped(InventoryProjectionVerificationStages.AcceptanceQuarantine,
            "The read-only verifier does not execute acceptance policy."),
        Skipped(InventoryProjectionVerificationStages.Persistence,
            "The read-only verifier never persists candidates."),
        Mapped(InventoryProjectionVerificationStages.SourceAccounting,
            "Source accounting executed against prepared candidates."),
        Skipped(InventoryProjectionVerificationStages.Publication,
            "The read-only verifier cannot publish inventory."),
    ];

    private static InventoryProjectionVerificationStageView Mapped(
        string stage,
        string reason) => new(
            stage, InventoryExtractionTraceCodes.Mapped, reason);

    private static InventoryProjectionVerificationStageView Skipped(
        string stage,
        string reason) => new(
            stage, InventoryExtractionTraceCodes.NotEvaluated, reason);

    private void EnsureSafeExecutionBoundary()
    {
        if (!processingOptions.Value.Paused)
        {
            throw new InvalidOperationException(
                "Inventory projection verification requires operational " +
                "processing to remain paused.");
        }
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.InventoryImport,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException(
                "Inventory access denied.");
        }
    }
}
