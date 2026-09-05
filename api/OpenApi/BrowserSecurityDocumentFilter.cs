using Advertified.Commercial.Api.Authentication;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Advertified.Commercial.Api.OpenApi;

public sealed class BrowserSecurityDocumentFilter(IConfiguration configuration) : IDocumentFilter
{
    private const string SchemeId = "browserSession";

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var settings = configuration.GetSection(BrowserSessionOptions.SectionName)
            .Get<BrowserSessionOptions>() ?? new BrowserSessionOptions();
        swaggerDoc.Components ??= new OpenApiComponents();
        swaggerDoc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        swaggerDoc.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie,
            Name = settings.CookieName,
            Description = "Authenticated browser session. Unsafe requests also require the CSRF header.",
        };
        foreach (var path in swaggerDoc.Paths)
        {
            if (!path.Key.StartsWith("/api/v1/tenants/", StringComparison.Ordinal) && path.Key != "/api/v1/me")
                continue;
            if (path.Value.Operations is null) continue;
            foreach (var operation in path.Value.Operations.Values)
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeId, swaggerDoc)] = [],
                }];
        }
    }
}
