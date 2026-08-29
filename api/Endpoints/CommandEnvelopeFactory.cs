using System.Security.Cryptography;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

internal static class CommandEnvelopeFactory
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string VersionHeader = "If-Match";

    public static CommandEnvelope<TCommand> Create<TCommand>(
        HttpContext context,
        TenantId tenantId,
        ActorId actorId,
        TCommand command,
        TimeProvider timeProvider,
        bool requireVersion)
        where TCommand : notnull
    {
        var idempotencyValue = context.Request.Headers[IdempotencyHeader].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyValue))
        {
            throw new IdempotencyKeyRequiredException();
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new CommandPayload<TCommand>(typeof(TCommand).Name, command));
        var digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        return new CommandEnvelope<TCommand>(
            tenantId,
            actorId,
            new CommandId(Guid.NewGuid()),
            new CorrelationId(Guid.Parse(context.TraceIdentifier)),
            new IdempotencyKey(idempotencyValue),
            new Sha256Digest(digest),
            requireVersion ? ReadExpectedVersion(context) : 0,
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

    private static long ReadExpectedVersion(HttpContext context)
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

        return long.TryParse(unquoted, out var version) && version > 0
            ? version
            : throw new ArgumentException("The record version is invalid.");
    }

    private sealed record CommandPayload<TCommand>(string Operation, TCommand Command);
}
