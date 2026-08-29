using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.Commercial.Api.Errors;

public sealed class HumanSafeProblemDetails : ProblemDetails
{
    public string Code { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }
}
