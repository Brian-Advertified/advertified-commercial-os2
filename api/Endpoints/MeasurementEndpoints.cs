using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static IEndpointRouteBuilder MapMeasurementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var campaigns = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/campaigns")
            .WithTags("Campaign measurement").RequireAuthorization();
        campaigns.MapPost("/{campaignId:guid}/performance-evidence", SubmitAsync)
            .WithName("SubmitPerformanceEvidence").Produces<PerformanceEvidenceView>()
            .WithCommandProblems(requiresVersion: false);
        campaigns.MapPost(
                "/{campaignId:guid}/performance-evidence/{evidenceId:guid}:review", ReviewAsync)
            .WithName("ReviewPerformanceEvidence").Produces<PerformanceEvidenceView>()
            .WithCommandProblems(requiresVersion: true);

        var evidence = endpoints.MapGroup(
                "/api/v1/tenants/{tenantId:guid}/performance-evidence")
            .WithTags("Campaign measurement").RequireAuthorization();
        evidence.MapGet("/{evidenceId:guid}", GetAsync)
            .WithName("GetPerformanceEvidence").Produces<PerformanceEvidenceView>()
            .WithQueryProblems();
        return endpoints;
    }

    private static Task<IResult> SubmitAsync(
        Guid tenantId, Guid campaignId, SubmitPerformanceEvidenceCommand command,
        HttpContext context, ICurrentIdentity identity, IPerformanceEvidenceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, false,
            (envelope, token) => commands.SubmitAsync(campaignId, envelope, token),
            cancellationToken);

    private static Task<IResult> ReviewAsync(
        Guid tenantId, Guid campaignId, Guid evidenceId,
        ReviewPerformanceEvidenceCommand command, HttpContext context,
        ICurrentIdentity identity, IPerformanceEvidenceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReviewAsync(
                campaignId, evidenceId, envelope, token), cancellationToken);

    private static async Task<IResult> GetAsync(
        Guid tenantId, Guid evidenceId, HttpContext context, ICurrentIdentity identity,
        IPerformanceEvidenceReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), evidenceId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }
}
