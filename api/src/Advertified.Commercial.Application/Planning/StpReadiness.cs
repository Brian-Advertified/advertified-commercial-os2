namespace Advertified.Commercial.Application.Planning;

public sealed record StpReadinessView(
    bool IsReady,
    decimal MinimumConfidence,
    decimal LowestConfidence,
    IReadOnlyList<string> Reasons);

public interface IStpReadinessEvaluator
{
    StpReadinessView Evaluate(
        AudienceDefinitionSetView strategy,
        decimal minimumConfidence);
}
