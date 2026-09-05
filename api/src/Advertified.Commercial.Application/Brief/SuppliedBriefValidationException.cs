namespace Advertified.Commercial.Application.Brief;

public sealed class SuppliedBriefValidationException(
    SuppliedBriefAgentUsageView usage, string responseJson, Exception innerException)
    : InvalidOperationException("The supplied Brief interpretation failed validation.", innerException)
{
    public SuppliedBriefAgentUsageView Usage { get; } = usage;
    public string ResponseJson { get; } = responseJson;
}
