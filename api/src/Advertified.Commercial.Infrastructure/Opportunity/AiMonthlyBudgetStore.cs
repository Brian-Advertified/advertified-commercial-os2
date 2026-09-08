using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AiMonthlyBudgetStore(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
{
    internal const long LimitUsdMicros = 5_000_000;

    internal async Task<AiBudgetReservation> ReserveAsync(
        AgentInvocationRequest invocation,
        CancellationToken cancellationToken)
    {
        var month = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
        month = new DateOnly(month.Year, month.Month, 1);
        var maximum = checked(
            invocation.ProviderPolicy.CostCapMinor * 10_000L);
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider
            .GetRequiredService<GovernanceDbContext>();
        var accepted = await database.Database.SqlQuery<bool>($"""
            SELECT governance.reserve_ai_monthly_budget(
                {month}, {invocation.RunId}, {invocation.StepId},
                {invocation.TenantId}, {maximum}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!accepted) throw new AiMonthlyBudgetExceededException();
        return new(month, invocation.RunId, invocation.StepId, maximum);
    }

    internal async Task CompleteAsync(
        AiBudgetReservation reservation,
        long actualUsdMicros,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider
            .GetRequiredService<GovernanceDbContext>();
        var completed = await database.Database.SqlQuery<bool>($"""
            SELECT governance.complete_ai_monthly_budget(
                {reservation.MonthStart}, {reservation.RunId},
                {reservation.StepId}, {actualUsdMicros}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!completed)
            throw new InvalidOperationException(
                "AI usage could not be reconciled with its reservation.");
    }
}

internal sealed record AiBudgetReservation(
    DateOnly MonthStart, Guid RunId, Guid StepId, long MaximumUsdMicros);

public sealed class AiMonthlyBudgetExceededException : Exception
{
}
