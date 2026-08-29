namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityEndpoints
{
    public static IEndpointRouteBuilder MapOpportunityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Opportunity qualification")
            .RequireAuthorization();
        group.MapOpportunityQueries();
        group.MapOpportunityCommands();
        group.MapOpportunityWorkflowCommands();
        group.MapOpportunityTaskCommands();
        return endpoints;
    }
}
