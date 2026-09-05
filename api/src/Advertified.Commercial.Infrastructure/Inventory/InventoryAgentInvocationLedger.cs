using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryAgentInvocationLedger(InventorySemanticStore store)
{
    internal async Task<AgentRuntimeResponse<T>> InvokeAsync<T>(InventorySemanticContext context,
        InventorySemanticRunRow run, long reservedCost, Func<CancellationToken, Task<AgentRuntimeResponse<T>>> invoke,
        CancellationToken cancellationToken)
    {
        if (run.Status == MasterDataCodes.LifecycleStatuses.Completed)
            return InventorySemanticStore.ReadResponse<T>(run);
        if (run.Status != MasterDataCodes.LifecycleStatuses.Pending)
            throw new InventorySemanticReconciliationRequiredException();
        await store.MarkRunningAsync(context, run, cancellationToken);
        try
        {
            var response = await invoke(cancellationToken);
            if (response.Usage.IncrementalCostUsdMicros < 0 || response.Usage.IncrementalCostUsdMicros > reservedCost)
                throw new InventorySemanticBudgetExceededException();
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await store.MarkCompletedAsync(context, run, response, completion.Token);
            return response;
        }
        catch (AgentRuntimeRejectedException rejected)
        {
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await store.MarkRejectedAsync(context, run, rejected, completion.Token);
            if (rejected.HasDefinitiveProviderAcceptance) throw new InventorySemanticResultRejectedException(rejected.Stage);
            throw new InventorySemanticReconciliationRequiredException();
        }
        catch
        {
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await store.MarkReconciliationRequiredAsync(context, run, "INVENTORY_AGENT_ACCEPTANCE_UNKNOWN", completion.Token);
            throw;
        }
    }
}
