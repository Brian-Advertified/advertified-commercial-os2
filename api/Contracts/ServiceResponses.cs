namespace Advertified.Commercial.Api;

public sealed record ServiceDescription(string Service, string Status, string Scope);

public sealed record HealthResponse(string Status, string Service, string[] Checks);
