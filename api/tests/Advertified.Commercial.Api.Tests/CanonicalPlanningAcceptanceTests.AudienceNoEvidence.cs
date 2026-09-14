using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task AudienceGenerationPersistsClientRequirementWithoutStructuredEvidence()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "UPDATE commercial.brief_versions SET audience_research_json = '[]'::jsonb WHERE id = $1",
                connection);
            command.Parameters.AddWithValue(BriefVersionId);
            await command.ExecuteNonQueryAsync();
        }

        await using var factory = CreateFactory(
            connectionString,
            OperatorId,
            configureServices: services =>
            {
                ConfigureDeterministicPlanningClock(services);
                services.RemoveAll<IAudienceIntelligenceAgentClient>();
                services.AddScoped<IAudienceIntelligenceAgentClient, RuntimeShapeAudienceAgentFixture>();
            });
        using var client = factory.CreateClient();
        using var audience = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/audiences:generate"),
            "planning-audience-no-structured-evidence", 1, new { });

        Assert.Equal("DRAFT", audience.RootElement.GetProperty("status").GetString());
        var definition = Assert.Single(audience.RootElement.GetProperty("definitions").EnumerateArray());
        Assert.Equal(MasterDataCodes.EvidenceClassifications.ClientRequirement,
            definition.GetProperty("classification").GetString());
        Assert.Equal(0, definition.GetProperty("evidenceItemIds").GetArrayLength());
        Assert.Equal(0, definition.GetProperty("referenceObservationIds").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, definition.GetProperty("confidence").ValueKind);
    }

    // HTTP wire validation is covered by AgentRuntimeHttpAdapterTests; this test
    // covers persistence without accessing the operator's running database/runtime.
    private sealed class RuntimeShapeAudienceAgentFixture : IAudienceIntelligenceAgentClient
    {
        public Task<AudienceAgentProposal> ProposeAudiencesAsync(
            AudienceIntelligenceInput input,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var audiences = input.Problem.Audiences.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new AudienceDefinitionProposal(
                    name,
                    $"Brief-supplied audience: {name}. Additional motivations, buying intent, affiliations and behaviours are not established unless separately supported by approved evidence.",
                    null, null, input.Problem.Geographies, null, null, null, null, null,
                    MasterDataCodes.EvidenceClassifications.ClientRequirement,
                    ["Do not infer sensitive individual attributes."], [], [], null, true))
                .ToArray();
            var names = string.Join(", ", audiences.Select(item => item.Name));
            var markets = string.Join(", ", input.Problem.Geographies);
            return Task.FromResult(new AudienceAgentProposal(
                audiences,
                $"Prioritise {names} for the stated objective within {markets}. This recommendation does not establish unstated consumer needs, buying contexts or behaviours; those remain research gaps.",
                null,
                [
                    "What verified consumer need, if any, is distinct from the campaign objective for each target audience?",
                    "What product, price, purchase occasion and decision-making role support each audience?",
                    "What evidence-backed proposition, benefit or message can support positioning?",
                    "Which dated aggregate audience studies establish media-use and location evidence?",
                    "Which approved evidence supports language, life-stage and segmentation labels?",
                ],
                "Audience proposals use only the approved Brief and keep unsupported detail unknown.",
                new IntelligenceInvocationUsage(MasterDataCodes.AgentTypes.AudienceIntelligence,
                    "deterministic", "fixture-v1", 0, "FIXTURE", null, 0, 0, 0)));
        }
    }
}
