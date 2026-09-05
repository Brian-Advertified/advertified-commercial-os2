using Advertified.Commercial.Application.Brief;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class DisabledSuppliedBriefAgentClient : ISuppliedBriefAgentClient
{
    public Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new SuppliedBriefInterpretationUnavailableException();
    }
}
