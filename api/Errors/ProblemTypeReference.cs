namespace Advertified.Commercial.Api.Errors;

internal static class ProblemTypeReference
{
    internal static string Create(string code) =>
        $"urn:advertified:problem:{code.ToLowerInvariant()}";
}
