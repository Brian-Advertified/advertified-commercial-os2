using Xunit;

namespace Advertified.Commercial.Api.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RecoveryTestGroup
{
    public const string Name = "recovery-containers";
}
