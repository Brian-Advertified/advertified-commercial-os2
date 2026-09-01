namespace Advertified.Commercial.Api.Startup;

public sealed class ProcessRoleOptions
{
    public const string SectionName = "Process";
    public const string ApiRole = "Api";
    public const string WorkerRole = "Worker";
    public const string CombinedRole = "Combined";

    public string Role { get; init; } = ApiRole;

    public bool RunsApi => Role is ApiRole or CombinedRole;

    public bool RunsWorkers => Role is WorkerRole or CombinedRole;

    public static bool IsSupported(ProcessRoleOptions options) =>
        options.Role is ApiRole or WorkerRole or CombinedRole;
}
