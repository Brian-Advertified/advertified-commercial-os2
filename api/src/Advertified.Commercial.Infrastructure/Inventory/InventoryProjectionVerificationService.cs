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
    string ProviderOutputHash,
    string CanonicalOutputHash,
    int ExtractedRowCount,
    IReadOnlyList<InventoryProjectionCandidateView> ProjectedCandidates,
    InventorySourceAccountingReport SourceAccounting);

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
                DateTimeOffset.UnixEpoch,
                source.FileName)
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
            extraction.ProviderOutputHash,
            extraction.CanonicalOutputHash,
            extraction.Rows.Count,
            views,
            extraction.Document.SourceAccounting ??
                throw new InvalidOperationException("Source accounting was not produced."));
    }

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
