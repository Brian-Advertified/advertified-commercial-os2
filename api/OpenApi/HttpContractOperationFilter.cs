using Advertified.Commercial.Api.Authentication;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Advertified.Commercial.Api.OpenApi;

public sealed class HttpContractOperationFilter : IOperationFilter
{
    private static readonly OpenApiSchema StringSchema = new()
    {
        Type = JsonSchemaType.String,
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        AddParameter(
            operation,
            "X-Correlation-ID",
            required: false,
            "Optional UUID request correlation identifier.");
        var method = context.ApiDescription.HttpMethod;
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        var isCommercialCommand = IsCommercialCommand(path, method);
        if (isCommercialCommand)
        {
            AddParameter(
                operation,
                "Idempotency-Key",
                required: true,
                "Unique key for safely retrying this command.");
        }

        var requiresVersion = method is "PUT" or "PATCH" ||
            context.ApiDescription.ActionDescriptor.EndpointMetadata
                .OfType<RequiresEntityVersionMetadata>().Any();
        if (isCommercialCommand && requiresVersion)
        {
            AddParameter(
                operation,
                "If-Match",
                required: true,
                "Strong ETag version returned by the latest read.");
        }

        if (method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            AddParameter(
                operation,
                BrowserSessionOptions.AntiforgeryHeaderName,
                required: operation.OperationId is "StartLocalBrowserSession"
                    or "EndBrowserSession"
                    or "EndOidcBrowserSession",
                "Required for unsafe cookie-authenticated browser requests.");
        }

        AddResponseHeaders(operation, path, method, isCommercialCommand);
    }

    private static void AddParameter(
        OpenApiOperation operation,
        string name,
        bool required,
        string description)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Schema = StringSchema,
        });
    }

    private static void AddResponseHeaders(
        OpenApiOperation operation,
        string path,
        string? method,
        bool isCommercialCommand)
    {
        if (operation.Responses is null)
        {
            return;
        }

        foreach (var responseEntry in operation.Responses)
        {
            if (responseEntry.Value is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["X-Correlation-ID"] = Header(
                "UUID support reference for this response.");
            if (!responseEntry.Key.StartsWith('2'))
            {
                continue;
            }

            if (ReturnsEntityVersion(path, method))
            {
                response.Headers["ETag"] = Header(
                    "Strong optimistic-concurrency version for this entity.");
            }

            if (isCommercialCommand)
            {
                response.Headers["Idempotency-Replayed"] = Header(
                    "True when a prior canonical command result was replayed.");
            }
        }
    }

    private static bool ReturnsEntityVersion(string path, string? method)
    {
        return IsCommercialCommand(path, method)
            || path.Equals("api/v1/me", StringComparison.Ordinal)
            || path.Equals("api/v1/tenants/{tenantId}", StringComparison.Ordinal);
    }

    private static bool IsCommercialCommand(string path, string? method) =>
        path.StartsWith("api/v1/tenants/", StringComparison.Ordinal)
        && (method is "POST" or "PUT" or "PATCH");

    private static OpenApiHeader Header(string description) => new()
    {
        Description = description,
        Schema = StringSchema,
    };
}
