namespace Advertified.Commercial.Application.Opportunity;

public sealed class AgentRuntimeUnavailableException : Exception
{
    public AgentRuntimeUnavailableException() : base("The agent runtime is unavailable.") { }
}
