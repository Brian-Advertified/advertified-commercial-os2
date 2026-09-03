using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public static class InventoryExtractionAttemptStateMachine
{
    public static readonly TimeSpan MaximumTaskDuration = TimeSpan.FromSeconds(3_600);

    private static readonly Dictionary<string, HashSet<string>> Transitions =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [MasterDataCodes.InventoryExtractionAttemptStatuses.Pending] = Set(
                MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled),
            [MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting] = Set(
                MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
                MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled),
            [MasterDataCodes.InventoryExtractionAttemptStatuses.Running] = Set(
                MasterDataCodes.InventoryExtractionAttemptStatuses.Completed,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut,
                MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
                MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled),
            [MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable] = Set(
                MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut,
                MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
                MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled),
            [MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired] = Set(
                MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled),
        };

    public static bool CanTransition(string current, string next) =>
        string.Equals(current, next, StringComparison.Ordinal) ||
        Transitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    public static void EnsureTransition(string current, string next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException(
                $"Invalid inventory extraction attempt transition: {current} -> {next}.");
        }
    }

    public static bool IsAutomaticallyClaimable(string status) => status is
        MasterDataCodes.InventoryExtractionAttemptStatuses.Pending or
        MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting or
        MasterDataCodes.InventoryExtractionAttemptStatuses.Running or
        MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable;

    public static bool HasTimedOut(DateTimeOffset submittedAtUtc, DateTimeOffset now) =>
        now - submittedAtUtc >= MaximumTaskDuration;

    private static HashSet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
