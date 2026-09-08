using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class NativeInventoryExtractionAdapter :
    IDurableInventoryDocumentExtractionAdapter
{
    public const string Name = "native-source-preprocessing";
    public const string Version = "1.0.0";

    public string ProviderName => Name;
    public string ProviderVersion => Version;
    public bool SupportsIdempotentSubmission => true;
    public bool SupportsCancellation => false;

    public Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            NativeInventorySourcePreprocessor.Process(request));
    }

    public Task<InventoryExtractionSubmission> SubmitAsync(
        InventoryExtractionRequest request,
        string stableSubmissionKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var taskId = "native:" + stableSubmissionKey;
        return Task.FromResult(new InventoryExtractionSubmission(
            taskId,
            InventoryProviderTaskState.Completed,
            "NATIVE_SOURCE_READY",
            "{\"state\":\"completed\"}"));
    }

    public Task<InventoryExtractionPollResult> PollAsync(
        string externalTaskId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new InventoryExtractionPollResult(
            InventoryProviderTaskState.Completed,
            "NATIVE_SOURCE_READY",
            null,
            "{\"state\":\"completed\"}"));
    }

    public Task<InventoryExtractionResult> ReadResultAsync(
        InventoryExtractionRequest request,
        string externalTaskId,
        CancellationToken cancellationToken) =>
        ExtractAsync(request, cancellationToken);

    public Task<bool> CancelAsync(
        string externalTaskId,
        CancellationToken cancellationToken) => Task.FromResult(false);
}
