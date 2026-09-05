using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

internal static class CommandEnvelopeFactory
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string VersionHeader = "If-Match";
    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        Converters = { new InventorySourceIdentityConverter() },
    };

    public static CommandEnvelope<TCommand> Create<TCommand>(
        HttpContext context,
        TenantId tenantId,
        ActorId actorId,
        TCommand command,
        TimeProvider timeProvider,
        bool requireVersion,
        bool allowZeroVersion = false)
        where TCommand : notnull
    {
        var idempotencyValue = context.Request.Headers[IdempotencyHeader].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyValue))
        {
            throw new IdempotencyKeyRequiredException();
        }

        var expectedVersion = requireVersion
            ? ReadExpectedVersion(context, allowZeroVersion)
            : 0;
        var operation = context.GetEndpoint()?.Metadata
            .GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new InvalidOperationException(
                "Command endpoints must declare a stable endpoint name.");
        }
        var routeValues = context.Request.RouteValues
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key,
                item => Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
        var digest = CommandPayloadDigest.Create(new CommandPayload<TCommand>(
            1, operation, tenantId.Value, actorId.Value, expectedVersion,
            routeValues, command), PayloadJson);
        return new CommandEnvelope<TCommand>(
            tenantId,
            actorId,
            new CommandId(Guid.NewGuid()),
            new CorrelationId(Guid.Parse(context.TraceIdentifier)),
            new IdempotencyKey(idempotencyValue),
            digest,
            expectedVersion,
            timeProvider.GetUtcNow(),
            command);
    }

    public static void SetEntityHeaders(HttpContext context, long version, bool replayed = false)
    {
        context.Response.Headers.ETag = $"\"{version}\"";
        if (replayed)
        {
            context.Response.Headers["Idempotency-Replayed"] = "true";
        }
    }

    private static long ReadExpectedVersion(HttpContext context, bool allowZeroVersion)
    {
        var value = context.Request.Headers[VersionHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PreconditionRequiredException();
        }

        var unquoted = value.Trim();
        if (unquoted.StartsWith('"') && unquoted.EndsWith('"') && unquoted.Length > 2)
        {
            unquoted = unquoted[1..^1];
        }

        var minimumVersion = allowZeroVersion ? 0 : 1;
        return long.TryParse(unquoted, out var version) && version >= minimumVersion
            ? version
            : throw new ArgumentException("The record version is invalid.");
    }

    private sealed record CommandPayload<TCommand>(
        int ProtocolVersion,
        string Operation,
        Guid TenantId,
        Guid ActorId,
        long ExpectedVersion,
        IReadOnlyDictionary<string, string?> RouteValues,
        TCommand Command);
}
