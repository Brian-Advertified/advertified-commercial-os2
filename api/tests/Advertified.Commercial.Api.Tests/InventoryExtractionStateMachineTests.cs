using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryExtractionStateMachineTests
{
    [Theory]
    [InlineData("PENDING", "SUBMITTING", true)]
    [InlineData("PENDING", "FAILED_TERMINAL", true)]
    [InlineData("PENDING", "RUNNING", false)]
    [InlineData("SUBMITTING", "RUNNING", true)]
    [InlineData("SUBMITTING", "RECONCILIATION_REQUIRED", true)]
    [InlineData("RUNNING", "FAILED_RETRYABLE", true)]
    [InlineData("RUNNING", "COMPLETED", true)]
    [InlineData("FAILED_RETRYABLE", "RUNNING", true)]
    [InlineData("FAILED_TERMINAL", "RUNNING", false)]
    [InlineData("TIMED_OUT", "CANCELLED", false)]
    [InlineData("TIMED_OUT", "RUNNING", false)]
    [InlineData("RECONCILIATION_REQUIRED", "RUNNING", true)]
    [InlineData("COMPLETED", "PENDING", false)]
    public void AllowsOnlyGovernedTransitions(string current, string next, bool expected)
    {
        Assert.Equal(expected,
            InventoryExtractionAttemptStateMachine.CanTransition(current, next));
    }
}
