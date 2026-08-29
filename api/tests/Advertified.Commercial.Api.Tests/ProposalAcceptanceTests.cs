using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class ProposalAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task AgencyPreparesBrandedProposalAndAssignedClientSelectsOneChoice()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_proposal", "advertified_proposal", "advertified-proposal-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        await using var operatorFactory = CreateFactory(connectionString, OperatorId);
        await using var clientFactory = CreateFactory(connectionString, ClientUserId);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId);
        using var agency = operatorFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        var planIds = new[] { Id("8a", 1, 1), Id("8a", 2, 1), Id("8a", 3, 1) };
        using var generated = await CommandAsync(
            agency,
            Path($"briefs/{BriefId}/proposals:generate"),
            "proposal-generate",
            null,
            new
            {
                title = "Three routes to qualified demand",
                options = new[]
                {
                    new { planVersionId = planIds[0], label = "Focused visibility", outcome = "Own the priority physical location." },
                    new { planVersionId = planIds[1], label = "Broadcast trust", outcome = "Build repeated audio reach and confidence." },
                    new { planVersionId = planIds[2], label = "Digital response", outcome = "Drive trackable enquiries through digital media." },
                },
                terms = "Rates and availability remain bound to the approved plan evidence.",
                expiryAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            });
        var proposalId = generated.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(3, generated.RootElement.GetProperty("options").GetArrayLength());
        Assert.Equal(3, generated.RootElement.GetProperty("options")
            .EnumerateArray().Select(item => item.GetProperty("planVersionId").GetGuid())
            .Distinct().Count());
        Assert.Equal("DRAFT", generated.RootElement.GetProperty("status").GetString());

        var options = generated.RootElement.GetProperty("options").EnumerateArray().Select(item => new
        {
            optionId = item.GetProperty("id").GetGuid(),
            label = item.GetProperty("label").GetString(),
            outcome = item.GetProperty("outcome").GetString(),
        }).ToArray();
        using var updated = await CommandAsync(
            agency,
            Path($"proposal-versions/{proposalId}:update"),
            "proposal-update",
            1,
            new
            {
                title = "Three routes to qualified demand",
                executiveSummary = "Choose the route that best matches the desired balance of visibility, trust and response.",
                terms = "Rates and availability remain bound to the approved plan evidence. Final booking follows client selection.",
                expiryAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                options,
            });
        Assert.Equal(2, updated.RootElement.GetProperty("version").GetInt64());

        using var approved = await CommandAsync(
            agency,
            Path($"proposal-versions/{proposalId}:approve"),
            "proposal-approve",
            2,
            new { reason = "Commercial wording and plan bindings reviewed." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());

        using var rendered = await CommandAsync(
            agency,
            Path($"proposal-versions/{proposalId}:render"),
            "proposal-render",
            3,
            new { });
        var document = rendered.RootElement.GetProperty("document");
        var documentId = document.GetProperty("id").GetGuid();
        Assert.Equal("application/pdf", document.GetProperty("mediaType").GetString());
        Assert.True(document.GetProperty("sizeBytes").GetInt64() > 100);

        using (var pdf = await agency.GetAsync(Path($"proposal-documents/{documentId}")))
        {
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        }

        using var shared = await CommandAsync(
            agency,
            Path($"proposal-versions/{proposalId}:share"),
            "proposal-share",
            4,
            new { recipientUserId = ClientUserId, reason = "Share for the first client decision." });
        Assert.Equal("SENT", shared.RootElement.GetProperty("status").GetString());
        Assert.Equal(ClientUserId, shared.RootElement.GetProperty("recipientUserId").GetGuid());

        using (var denied = await other.GetAsync(Path($"proposals/{proposalId}")))
        {
            await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        }
        using (var deniedDecision = await RawCommandAsync(
            other,
            Path($"proposal-versions/{proposalId}:select-option"),
            "proposal-other-select",
            5,
            new { optionId = options[0].optionId, reason = "Not assigned." }))
        {
            await AssertProblemAsync(deniedDecision, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        }

        using var clientView = JsonDocument.Parse(await client.GetStringAsync(Path($"proposals/{proposalId}")));
        Assert.Equal("SENT", clientView.RootElement.GetProperty("status").GetString());
        Assert.False(clientView.RootElement.TryGetProperty("supplierCostMinor", out _));
        using var selected = await CommandAsync(
            client,
            Path($"proposal-versions/{proposalId}:select-option"),
            "proposal-client-select",
            5,
            new { optionId = options[2].optionId, reason = "Digital response best matches the campaign objective." });
        Assert.Equal("SELECTED", selected.RootElement.GetProperty("status").GetString());
        Assert.Equal(options[2].optionId,
            selected.RootElement.GetProperty("decision").GetProperty("optionId").GetGuid());

        using var repeated = await RawCommandAsync(
            client,
            Path($"proposal-versions/{proposalId}:decline"),
            "proposal-client-decline-after-select",
            6,
            new { reason = "Attempted second decision." });
        await AssertProblemAsync(repeated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task DuplicateOrExpiredProposalChoicesFailClosed()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_proposal_negative", "advertified_proposal", "advertified-proposal-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var operatorFactory = CreateFactory(connectionString, OperatorId);
        using var agency = operatorFactory.CreateClient();
        var planId = Id("8a", 1, 1);

        using var duplicate = await RawCommandAsync(
            agency,
            Path($"briefs/{BriefId}/proposals:generate"),
            "proposal-duplicate-plans",
            null,
            new
            {
                title = "Invalid duplicate choices",
                options = new[]
                {
                    new { planVersionId = planId, label = "Choice one", outcome = "Same plan." },
                    new { planVersionId = planId, label = "Choice two", outcome = "Still the same plan." },
                },
                terms = "Invalid fixture.",
                expiryAtUtc = DateTimeOffset.UtcNow.AddDays(10),
            });
        await AssertProblemAsync(duplicate, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        using var expired = await RawCommandAsync(
            agency,
            Path($"briefs/{BriefId}/proposals:generate"),
            "proposal-expired",
            null,
            new
            {
                title = "Expired proposal",
                options = new[]
                {
                    new { planVersionId = planId, label = "Choice", outcome = "Expired." },
                },
                terms = "Invalid fixture.",
                expiryAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
        await AssertProblemAsync(expired, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }
}
