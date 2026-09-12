using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySchemaInterpreterAcceptanceTests
{
    private const string Model = "amazon.nova-lite-v1:0";
    private static readonly Guid TenantId =
        Guid.Parse("7a100000-0000-0000-0000-000000000001");
    private static readonly Guid UserId =
        Guid.Parse("7a200000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId =
        Guid.Parse("7a300000-0000-0000-0000-000000000001");
    private static readonly Guid AttemptId =
        Guid.Parse("7a400000-0000-0000-0000-000000000001");
    private static readonly Guid CorrelationId =
        Guid.Parse("7a500000-0000-0000-0000-000000000001");
    private static readonly string SourceHash = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task SchemaDiscoveryIsDurableAndProjectsWithoutSecondProviderCall()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_schema_discovery",
            "advertified_schema_discovery",
            "advertified-schema-discovery-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        var calls = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Task.FromResult(Response());
        })) { BaseAddress = new Uri("https://agent-runtime.test") };
        var runtime = Runtime();
        var semantic = Semantic();
        var store = new InventorySemanticStore(db, TimeProvider.System);
        var client = new InventorySemanticAgentClient(
            http, Options.Create(runtime));
        var interpreter = new InventorySchemaInterpreter(
            store,
            client,
            Options.Create(semantic),
            Options.Create(runtime));
        var request = Request();

        var first = await interpreter.DiscoverAsync(
            request, CancellationToken.None);
        var replay = await interpreter.DiscoverAsync(
            request, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(first.ProtocolVersion, replay.ProtocolVersion);
        Assert.Equal(first.SourceHash, replay.SourceHash);
        Assert.Equal(first.StructureHash, replay.StructureHash);
        Assert.Equal(first.Confidence, replay.Confidence);
        Assert.Equal(first.Records.Count, replay.Records.Count);
        Assert.Equal(first.Records[0].SourceStructure,
            replay.Records[0].SourceStructure);
        Assert.Equal(first.Provenance, replay.Provenance);
        Assert.Equal("inventory-schema-discovery", first.Provenance.Interpreter);
        Assert.Equal(Model, first.Provenance.Model);
        Assert.Equal("schema-provider-request-1", first.Provenance.ProviderRequestId);
        Assert.Equal(1_000, first.Provenance.CostUsdMicros);
        var document = new InventoryDocumentStructure(
            request.SourceHash, request.StructureHash,
            request.RepresentativeStructures);
        var rows = InventorySchemaProjection.Project(
            document,
            new InventorySchemaProposal(
                first.ProtocolVersion, first.SourceHash, first.StructureHash,
                first.Records, first.Confidence, first.Warnings),
            request.GovernedCodes);
        Assert.Equal(2, rows.Count);
        Assert.Equal("SITE-A", Value(rows[0], "product_code"));
        Assert.Equal("98000", Value(rows[1], "rate"));
        await AssertSingleDurableRunAsync(connectionString);
    }

    private static InventorySchemaDiscoveryRequest Request()
    {
        var cells = new[]
        {
            new InventorySourceCell("h-product", 1, 1, "Product"),
            new InventorySourceCell("h-rate", 1, 2, "Rate"),
            new InventorySourceCell("r2-product", 2, 1, "SITE-A"),
            new InventorySourceCell("r2-rate", 2, 2, "125000"),
            new InventorySourceCell("r3-product", 3, 1, "SITE-B"),
            new InventorySourceCell("r3-rate", 3, 2, "98000"),
        };
        var structure = new InventorySourceStructure("sheet-1", "cell", cells);
        var document = new InventoryDocumentStructure(
            SourceHash,
            new string('b', 64),
            [structure]);
        return new InventorySchemaDiscoveryRequest(
            "inventory-schema/1.0",
            document.SourceHash,
            document.StructureHash,
            document.Structures,
            new HashSet<string>(["product_code", "rate"], StringComparer.Ordinal),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            new InventorySchemaExecutionContext(
                TenantId, UserId, ImportId, 1, AttemptId, CorrelationId));
    }

    private static AgentRuntimeOptions Runtime()
    {
        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var agent in new[]
        {
            MasterDataCodes.AgentTypes.BusinessInterpretation,
            MasterDataCodes.AgentTypes.OpportunityIntelligence,
            MasterDataCodes.AgentTypes.Strategy,
            MasterDataCodes.AgentTypes.CriticReadiness,
            MasterDataCodes.AgentTypes.BriefDrafting,
            MasterDataCodes.AgentTypes.MarketIntelligence,
            MasterDataCodes.AgentTypes.AudienceIntelligence,
            MasterDataCodes.AgentTypes.LocationIntelligence,
            MasterDataCodes.AgentTypes.MediaStrategy,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataCodes.AgentTypes.ProposalNarrative,
            MasterDataCodes.AgentTypes.Creative,
            MasterDataCodes.AgentTypes.Measurement,
        })
            models[agent] = Model;
        models[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.BriefDrafting,
            "SUPPLIED_BRIEF_UNDERSTANDING")] = Model;
        models[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            InventoryExtractionTraceCodes.SchemaDiscovery)] = Model;
        models[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            InventorySemanticOperations.SourceTranscription)] = Model;
        models[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            InventorySemanticOperations.SemanticEnrichment)] = Model;
        return new AgentRuntimeOptions
        {
            Mode = AgentRuntimeOptions.HttpMode,
            BaseUrl = "https://agent-runtime.test",
            ServiceKey = "schema-test-service-key",
            Provider = AgentRuntimeOptions.BedrockProvider,
            DefaultCostCapMinor = 1,
            AllowLive = true,
            MaxAttempts = 1,
            Models = models,
        };
    }

    private static InventorySemanticOptions Semantic() => new()
    {
        Enabled = true,
        ModelId = Model,
        InputPricePerMillionTokensUsdMicros = 60_000,
        OutputPricePerMillionTokensUsdMicros = 240_000,
        PerCallCostCapUsdMicros = 10_000,
        CertificationBudgetUsdMicros = 5_000_000,
        BudgetScope = "schema-test-budget",
        PromptVersion = "5.0.0",
        MaximumOutputTokensPerChunk = 1_024,
    };

    private static HttpResponseMessage Response()
    {
        var mapping = new Func<string, string, string, int, object>(
            (meaning, label, locator, column) => new
            {
                canonical_meaning = meaning,
                source_label = label,
                source_location = locator,
                source_structure = "sheet-1",
                source_column = column,
                row_offset = 0,
                is_document_metadata = false,
                interpretation = $"{label} identifies {meaning}.",
                confidence = 1m,
                evidence = new[]
                {
                    new { source_locator = locator, quoted_text = label },
                },
                interpreted_code = (string?)null,
                value_source_location = (string?)null,
            });
        var artifact = new
        {
            protocol_version = "inventory-schema/1.0",
            source_hash = SourceHash,
            structure_hash = new string('b', 64),
            records = new[]
            {
                new
                {
                    source_structure = "sheet-1",
                    record_boundary = new
                    {
                        first_row = 2,
                        last_row = 3,
                        rows_per_record = 1,
                        excluded_rows = Array.Empty<int>(),
                        exclusion_reasons = (object?)null,
                    },
                    field_mappings = new[]
                    {
                        mapping("product_code", "Product", "h-product", 1),
                        mapping("rate", "Rate", "h-rate", 2),
                    },
                    supplier_metadata_mappings = Array.Empty<object>(),
                    asset_mappings = Array.Empty<object>(),
                },
            },
            confidence = 1m,
            warnings = Array.Empty<string>(),
        };
        var json = JsonSerializer.Serialize(new
        {
            schema_version = "1.0.0",
            status = MasterDataCodes.LifecycleStatuses.Completed,
            artifact,
            evidence_bindings = Array.Empty<object>(),
            unknowns = Array.Empty<object>(),
            assumptions = Array.Empty<object>(),
            confidence = Array.Empty<object>(),
            objections = Array.Empty<object>(),
            rationale = "Schema uses only supplied source cells.",
            suggested_next_action = (object?)null,
            usage = new
            {
                provider = AgentRuntimeOptions.BedrockProvider,
                model = Model,
                units = 200,
                tool_calls = 0,
                incremental_cost_minor = 1,
                cache_status = "LIVE",
                provider_request_id = "schema-provider-request-1",
                input_tokens = 100,
                output_tokens = 100,
                incremental_cost_usd_micros = 1_000,
            },
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO commercial.tenants (
                id, type_code, legal_name, trading_name, slug, status_code,
                timezone, currency_code, vat_status_code, settings_json,
                version, created_at_utc, updated_at_utc)
            VALUES (@tenant, 'AGENCY', 'Schema Agency', 'Schema Agency',
                'schema-agency', 'ACTIVE', 'Africa/Johannesburg', 'ZAR',
                'REGISTERED', '{}'::jsonb, 1, @now, @now);
            INSERT INTO commercial.users (
                id, email, display_name, status_code, mfa_enabled,
                version, created_at_utc, updated_at_utc)
            VALUES (@user, 'schema@example.test', 'Schema Operator', 'ACTIVE',
                true, 1, @now, @now);
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code,
                status_code, scan_status_code, quarantine_object_key,
                protected_object_key, source_hash, source_size, created_by,
                version, created_at_utc, updated_at_utc)
            VALUES (@import, @tenant, 'unseen.xlsx',
                'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                'documentClasses', 'XLSX', 'EXTRACTING', 'CLEAN',
                'quarantine/unseen.xlsx', 'protected/unseen.xlsx', @hash,
                128, @user, 1, @now, @now);
            INSERT INTO commercial.inventory_extraction_attempts (
                id, tenant_id, import_id, source_file_version, source_hash,
                stable_submission_key, provider_name, provider_version,
                status_code, polling_checkpoint, attempt_number, correlation_id,
                command_id, requested_by, version, created_at_utc, updated_at_utc)
            VALUES (@attempt, @tenant, @import, 1, @hash,
                'schema-discovery-attempt', 'native-source-preprocessing', '1.0.0',
                'RUNNING', '{}'::jsonb, 1, @correlation, gen_random_uuid(),
                @user, 1, @now, @now);
            """, connection);
        command.Parameters.AddWithValue("tenant", TenantId);
        command.Parameters.AddWithValue("user", UserId);
        command.Parameters.AddWithValue("import", ImportId);
        command.Parameters.AddWithValue("attempt", AttemptId);
        command.Parameters.AddWithValue("correlation", CorrelationId);
        command.Parameters.AddWithValue("hash", SourceHash);
        command.Parameters.AddWithValue("now", Now);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertSingleDurableRunAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*), min(status_code), min(provider_request_id),
                min(incremental_cost_usd_micros)
            FROM commercial.inventory_semantic_runs
            WHERE import_id = @import
            """, connection);
        command.Parameters.AddWithValue("import", ImportId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Completed, reader.GetString(1));
        Assert.Equal("schema-provider-request-1", reader.GetString(2));
        Assert.Equal(1_000L, reader.GetInt64(3));
    }

    private static string Value(InventoryExtractedRow row, string meaning) =>
        Assert.Single(row.DiscoveredFields!, item =>
            item.CanonicalMeaning == meaning).RawValue;

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
