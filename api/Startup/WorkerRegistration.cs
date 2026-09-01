using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Worker;

namespace Advertified.Commercial.Api.Startup;

internal static class WorkerRegistration
{
    internal static ProcessRoleOptions AddAdvertifiedProcessRole(
        this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(ProcessRoleOptions.SectionName);
        var role = section.Get<ProcessRoleOptions>() ?? new ProcessRoleOptions();
        if (!ProcessRoleOptions.IsSupported(role))
        {
            throw new InvalidOperationException("The Advertified process role is invalid.");
        }
        builder.Services.AddOptions<ProcessRoleOptions>()
            .Bind(section)
            .Validate(ProcessRoleOptions.IsSupported, "The Advertified process role is invalid.")
            .ValidateOnStart();
        return role;
    }

    internal static void AddCommercialWorkers(
        this WebApplicationBuilder builder,
        ProcessRoleOptions processRole)
    {
        if (!processRole.RunsWorkers)
        {
            return;
        }
        var workerConnection = builder.Configuration
            .GetConnectionString("WorkerSchedulerDatabase");
        if (string.IsNullOrWhiteSpace(workerConnection))
        {
            throw new InvalidOperationException(
                "Worker processes require the scheduler database connection.");
        }
        var email = builder.Configuration
            .GetSection(EmailAutomationOptions.SectionName)
            .Get<EmailAutomationOptions>() ?? new EmailAutomationOptions();
        if (email.Mode != EmailAutomationOptions.DisabledMode && email.ProcessInline)
        {
            throw new InvalidOperationException(
                "Worker-managed email automation must disable inline processing.");
        }

        var section = builder.Configuration.GetSection(WorkerDispatchOptions.SectionName);
        builder.Services.AddOptions<WorkerDispatchOptions>()
            .Bind(section)
            .Validate(WorkerDispatchOptions.HasSafeTiming,
                "The worker dispatch timing is invalid.")
            .ValidateOnStart();
        builder.Services.AddSingleton(new WorkerSchedulerStore(workerConnection));
        builder.Services.AddHostedService<CommercialWorkerService>();
    }
}
