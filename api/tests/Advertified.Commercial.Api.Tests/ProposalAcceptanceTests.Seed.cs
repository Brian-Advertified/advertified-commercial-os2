using Npgsql;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class ProposalAcceptanceTests
{
    private static readonly Guid AudienceSetId = Guid.Parse("86000000-0000-0000-0000-000000000001");
    private static readonly Guid SupplierId = Guid.Parse("87000000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId = Guid.Parse("87000000-0000-0000-0000-000000000002");
    private static readonly Guid CandidateId = Guid.Parse("87000000-0000-0000-0000-000000000003");

    private static async Task SeedProposalPrerequisitesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var batch = new NpgsqlBatch(connection))
        {
            Add(batch, """
                INSERT INTO commercial.campaign_briefs (
                    id, tenant_id, client_account_id, title, owner_user_id, status_code,
                    version, created_at_utc, updated_at_utc)
                VALUES ($1, $2, $3, 'Growth campaign', $4, 'APPROVED', 1, $5, $5)
                """, BriefId, TenantId, ClientId, OperatorId, Now);
            Add(batch, """
                INSERT INTO commercial.brief_sources (
                    id, tenant_id, brief_id, source_type_code, locator, title, content,
                    content_hash, created_by, created_at_utc)
                VALUES ($1, $2, $3, 'SUPPLIED_TEXT', 'proposal:test', 'Approved source',
                    'Approved campaign source', repeat('a', 64), $4, $5)
                """, BriefSourceId, TenantId, BriefId, OperatorId, Now);
            Add(batch, """
                INSERT INTO commercial.brief_versions (
                    id, tenant_id, brief_id, source_id, version_no, business_problem,
                    objective, audiences_json, geographies_json, timing, budget_minor,
                    budget_unknown, currency_code, vat_status_code, fees_minor,
                    constraints_json, measurement_json, facts_json, unknowns_json,
                    assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                    created_by, approved_by, approved_at_utc, version, created_at_utc)
                VALUES ($1, $2, $3, $4, 1, 'Grow qualified demand', 'Increase qualified enquiries',
                    '["Business buyers"]', '["Johannesburg"]', 'September to November 2026',
                    35000000, false, 'ZAR', 'REGISTERED', 0, '[]', '[]', '[]', '[]', '[]',
                    '[]', '[]', 'APPROVED', $5, $5, $6, 1, $6)
                """, BriefVersionId, TenantId, BriefId, BriefSourceId, OperatorId, Now);
            Add(batch, """
                UPDATE commercial.campaign_briefs
                SET current_draft_version_id = $1, approved_version_id = $1 WHERE id = $2
                """, BriefVersionId, BriefId);
            Add(batch, """
                INSERT INTO commercial.audience_definition_sets (
                    id, tenant_id, brief_version_id, version_no, input_hash, status_code,
                    created_by, created_at_utc)
                VALUES ($1, $2, $3, 1, repeat('b', 64), 'APPROVED', $4, $5)
                """, AudienceSetId, TenantId, BriefVersionId, OperatorId, Now);
            Add(batch, """
                INSERT INTO commercial.inventory_suppliers (
                    id, tenant_id, name, version, created_at_utc, updated_at_utc)
                VALUES ($1, $2, 'Proposal Media', 1, $3, $3)
                """, SupplierId, TenantId, Now);
            Add(batch, """
                INSERT INTO commercial.inventory_imports (
                    id, tenant_id, supplier_id, source_file_name, declared_media_type,
                    document_class_collection_code, document_class_code, status_code,
                    scan_status_code, quarantine_object_key, protected_object_key,
                    source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
                VALUES ($1, $2, $3, 'proposal.csv', 'text/csv', 'documentClasses', 'CSV',
                    'COMPLETED', 'CLEAN', 'q/proposal', 'p/proposal', repeat('c', 64), 100,
                    $4, 2, $5, $5)
                """, ImportId, TenantId, SupplierId, OperatorId, Now);
            Add(batch, """
                INSERT INTO commercial.inventory_candidates (
                    id, tenant_id, import_id, row_number, status_code, proposed_values_json,
                    canonical_values_json, validation_json, source_locator, reviewed_by,
                    version, created_at_utc, updated_at_utc)
                VALUES ($1, $2, $3, 1, 'APPROVED', '{}', '{}', '[]', 'csv#row=2',
                    $4, 1, $5, $5)
                """, CandidateId, TenantId, ImportId, OperatorId, Now);
            await batch.ExecuteNonQueryAsync();
        }

        var routes = new[]
        {
            new RouteSeed("OOH", "OOH_SITE", "Focused visibility", 10_000_000L, 1),
            new RouteSeed("RADIO", "RADIO_SPOT", "Audio reach", 20_000_000L, 2),
            new RouteSeed("DIGITAL", "DIGITAL_PLACEMENT", "Digital response", 35_000_000L, 3),
        };
        foreach (var route in routes)
        {
            await SeedRouteAsync(connection, route);
        }
    }

    private static async Task SeedRouteAsync(NpgsqlConnection connection, RouteSeed route)
    {
        var productId = Id("88", route.Ordinal, 1);
        var productVersionId = Id("88", route.Ordinal, 2);
        var rateId = Id("88", route.Ordinal, 3);
        var availabilityId = Id("88", route.Ordinal, 4);
        var mixId = Id("89", route.Ordinal, 1);
        var shortlistId = Id("89", route.Ordinal, 2);
        var shortlistCandidateId = Id("89", route.Ordinal, 3);
        var planId = Id("8a", route.Ordinal, 1);
        var planLineId = Id("8a", route.Ordinal, 2);
        await using var batch = new NpgsqlBatch(connection);
        Add(batch, """
            INSERT INTO commercial.inventory_products (
                id, tenant_id, supplier_id, supplier_product_code, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, $4, 'ACTIVE', 1, $5, $5)
            """, productId, TenantId, SupplierId, $"ROUTE-{route.Ordinal}", Now);
        Add(batch, """
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code,
                product_type_code, geography, latitude, longitude, verification_code,
                source_import_id, source_candidate_id, published_by, published_at_utc)
            VALUES ($1, $2, $3, 1, $4, $5, $6, 'Johannesburg', -26.2041, 28.0473,
                'HUMAN_VERIFIED', $7, $8, $9, $10)
            """, productVersionId, TenantId, productId, route.Name, route.Channel,
            route.ProductType, ImportId, CandidateId, OperatorId, Now);
        Add(batch, "UPDATE commercial.inventory_products SET current_version_id = $1 WHERE id = $2",
            productVersionId, productId);
        Add(batch, """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', $4, '2026-01-01', '2027-12-31', 'csv#row=2')
            """, rateId, TenantId, productVersionId, route.AmountMinor);
        Add(batch, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code, observed_at_utc,
                valid_until_utc, source_locator)
            VALUES ($1, $2, $3, 'AVAILABLE', $4, $5, 'supplier-confirmation')
            """, availabilityId, TenantId, productVersionId, Now, Now.AddYears(1));
        Add(batch, """
            INSERT INTO commercial.media_mix_versions (
                id, tenant_id, brief_version_id, audience_set_id, version_no,
                total_budget_minor, currency_code, allocations_json, channel_roles_json,
                assumptions_json, evidence_item_ids_json, input_hash, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            VALUES ($1, $2, $3, $4, $5, 35000000, 'ZAR', $6::jsonb, $7::jsonb,
                '[]', '[]', repeat('d', 64), 'APPROVED', $8, $8, $9, 2, $9)
            """, mixId, TenantId, BriefVersionId, AudienceSetId, route.Ordinal,
            $"[{{\"channel\":\"{route.Channel}\",\"budgetMinor\":35000000,\"role\":\"{route.Name}\",\"runningPeriods\":[{{\"start\":\"2026-09-01\",\"end\":\"2026-09-30\"}}]}}]",
            $"{{\"{route.Channel}\":\"{route.Name}\"}}", OperatorId, Now);
        Add(batch, """
            INSERT INTO commercial.inventory_shortlist_versions (
                id, tenant_id, brief_version_id, mix_version_id, version_no,
                input_hash, assumptions_json, status_code, created_by, version, created_at_utc)
            VALUES ($1, $2, $3, $4, $5, repeat('e', 64), '[]', 'APPROVED', $6, 2, $7)
            """, shortlistId, TenantId, BriefVersionId, mixId, route.Ordinal, OperatorId, Now);
        Add(batch, """
            INSERT INTO commercial.inventory_shortlist_candidates (
                id, tenant_id, shortlist_version_id, inventory_tenant_id,
                inventory_product_id, product_version_id, rate_id, availability_id,
                product_name, is_eligible, score, rate_amount_minor, currency_code,
                channel_code, geography, input_hash, created_at_utc)
            VALUES ($1, $2, $3, $2, $4, $5, $6, $7, $8, true, 90, $9, 'ZAR', $10,
                'Johannesburg', repeat('f', 64), $11)
            """, shortlistCandidateId, TenantId, shortlistId, productId, productVersionId,
            rateId, availabilityId, route.Name, route.AmountMinor, route.Channel, Now);
        Add(batch, """
            INSERT INTO commercial.media_plan_versions (
                id, tenant_id, brief_version_id, mix_version_id, shortlist_version_id,
                version_no, subtotal_minor, fees_minor, vat_minor, total_minor, currency_code,
                forecast_json, assumptions_json, supply_confidence_code, critic_report_json,
                input_hash, status_code, created_by, approved_by, approved_at_utc,
                version, created_at_utc)
            VALUES ($1, $2, $3, $4, $5, $6, $7, 0, 0, $7, 'ZAR', '{}', '[]',
                'CONFIRMED', '[]', $8, 'APPROVED', $9, $9, $10, 2, $10)
            """, planId, TenantId, BriefVersionId, mixId, shortlistId, route.Ordinal,
            route.AmountMinor, new string((char)('0' + route.Ordinal), 64), OperatorId, Now);
        Add(batch, """
            INSERT INTO commercial.media_plan_lines (
                id, tenant_id, plan_version_id, shortlist_candidate_id, inventory_tenant_id,
                inventory_product_id, product_version_id, rate_id, availability_id,
                product_name, channel_code, geography, flight_start, flight_end,
                running_periods_json, quantity, supplier_cost_minor, client_price_minor,
                fees_minor, vat_minor, forecast_json, input_hash)
            VALUES ($1, $2, $3, $4, $2, $5, $6, $7, $8, $9, $10, 'Johannesburg',
                '2026-09-01', '2026-09-30',
                '[{"start":"2026-09-01","end":"2026-09-30"}]', 1, $11, $11, 0, 0,
                '{}', repeat('a', 64))
            """, planLineId, TenantId, planId, shortlistCandidateId, productId,
            productVersionId, rateId, availabilityId, route.Name, route.Channel,
            route.AmountMinor);
        Add(batch, """
            INSERT INTO commercial.supply_coordination (
                id, tenant_id, media_plan_line_id, supplier_tenant_id,
                supplier_id, availability_code,
                rate_freshness_code, last_confirmed_at_utc, source_locator, status_code)
            VALUES ($1, $2, $3, $2, $4, 'AVAILABLE', 'CURRENT', $5,
                'supplier-confirmation', 'ACTIVE')
            """, Id("8b", route.Ordinal, 1), TenantId, planLineId, SupplierId, Now);
        await batch.ExecuteNonQueryAsync();
    }

    private static Guid Id(string prefix, int ordinal, int suffix) => Guid.Parse(
        $"{prefix}000000-{ordinal:D4}-0000-0000-{suffix:D12}");

    private static void Add(NpgsqlBatch batch, string sql, params object[] parameters)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in parameters) command.Parameters.AddWithValue(value);
        batch.BatchCommands.Add(command);
    }

    private sealed record RouteSeed(
        string Channel,
        string ProductType,
        string Name,
        long AmountMinor,
        int Ordinal);
}
