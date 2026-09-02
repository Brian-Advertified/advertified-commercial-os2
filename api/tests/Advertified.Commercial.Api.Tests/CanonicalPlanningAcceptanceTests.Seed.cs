using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task SeedPlanningPrerequisitesAsync(
        string connectionString,
        long briefBudgetMinor)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, title, owner_user_id, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'Johannesburg launch', $4, 'APPROVED', 1, $5, $5)
            """, BriefId, TenantId, ClientId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title, content,
                content_hash, created_by, created_at_utc)
            VALUES ($1, $2, $3, 'SUPPLIED_TEXT', 'owner:supplied', 'Approved source',
                'Approved campaign source', repeat('a', 64), $4, $5)
            """, BriefSourceId, TenantId, BriefId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            VALUES ($1, $2, $3, $4, 1, 'Create qualified local demand',
                'Increase qualified enquiries', '["Local business decision makers"]',
                '["Johannesburg"]', 'September 2026', $7, false, 'ZAR',
                'REGISTERED', 5000, '[]', '[]', '["Owner supplied objective"]',
                '[]', '[]', '[]', '[]', 'APPROVED', $5, $5, $6, 1, $6)
            """, BriefVersionId, TenantId, BriefId, BriefSourceId, OperatorId, Now,
            briefBudgetMinor);
        AddCommand(batch,
            """
            UPDATE commercial.campaign_briefs
            SET current_draft_version_id = $1, approved_version_id = $1
            WHERE id = $2
            """, BriefVersionId, BriefId);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, 'Planning Media', 1, $3, $3)
            """, SupplierId, TenantId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code, status_code,
                scan_status_code, quarantine_object_key, protected_object_key,
                source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'planning.csv', 'text/csv', 'documentClasses', 'CSV',
                'COMPLETED', 'CLEAN', 'q/planning', 'p/planning', repeat('b', 64), 100,
                $4, 2, $5, $5)
            """, ImportId, TenantId, SupplierId, OperatorId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code, proposed_values_json,
                canonical_values_json, validation_json, source_locator, reviewed_by,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 1, 'APPROVED', '{}', '{}', '[]', 'csv#row=2',
                $4, 1, $5, $5)
            """, CandidateId, TenantId, ImportId, OperatorId, Now);
        var supplierVersionId = Guid.Parse("75000000-0000-0000-0000-000000000004");
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_supplier_versions (
                id, tenant_id, supplier_id, version_number, vat_status_code,
                vat_number, commission_terms, payment_terms, cancellation_terms,
                booking_deadline_terms, source_import_id, published_by, published_at_utc)
            VALUES ($1, $2, $3, 1, 'REGISTERED', '4123456789',
                'No supplier commission is included.', 'Payment within 30 days.',
                'Cancellation fees apply after confirmation.',
                'Book five business days before start.', $4, $5, $6)
            """, supplierVersionId, TenantId, SupplierId, ImportId, OperatorId, Now);
        AddCommand(batch,
            "UPDATE commercial.inventory_suppliers SET current_commercial_version_id = $1 " +
            "WHERE tenant_id = $2 AND id = $3",
            supplierVersionId, TenantId, SupplierId);
        var policyId = Guid.Parse("75000000-0000-0000-0000-000000000005");
        var policyVersionId = Guid.Parse("75000000-0000-0000-0000-000000000006");
        AddCommand(batch,
            """
            INSERT INTO commercial.commercial_policies (
                id, tenant_id, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, 1, $3, $3)
            """, policyId, TenantId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.commercial_policy_versions (
                id, tenant_id, policy_id, version_number, markup_basis_points,
                management_fee_basis_points, commission_basis_points, vat_status_code,
                vat_rate_basis_points, prices_include_vat, currency_code,
                booking_approval_threshold_minor, allow_self_approval, created_by,
                created_at_utc)
            VALUES ($1, $2, $3, 1, 0, 500, 0, 'REGISTERED', 1500, false,
                'ZAR', 1000000, true, $4, $5)
            """, policyVersionId, TenantId, policyId, OperatorId, Now);
        AddCommand(batch,
            "UPDATE commercial.commercial_policies SET current_version_id = $1 " +
            "WHERE tenant_id = $2 AND id = $3",
            policyVersionId, TenantId, policyId);
        await batch.ExecuteNonQueryAsync();

        var rates = new long[] { 100_000, 120_000, 140_000, 160_000, 180_000, 90_000 };
        for (var index = 0; index < rates.Length; index++)
        {
            await InsertProductAsync(connection, index, rates[index]);
        }
    }

    private static async Task InsertProductAsync(
        NpgsqlConnection connection,
        int index,
        long rate)
    {
        var productId = Guid.Parse($"77000000-0000-0000-0000-{index + 1:D12}");
        var versionId = Guid.Parse($"78000000-0000-0000-0000-{index + 1:D12}");
        var rateId = Guid.Parse($"79000000-0000-0000-0000-{index + 1:D12}");
        var availabilityId = Guid.Parse($"7a000000-0000-0000-0000-{index + 1:D12}");
        var geography = index == 5 ? "Cape Town" : "Johannesburg";
        var effectiveTo = index == 4
            ? new DateOnly(2026, 8, 31)
            : new DateOnly(2027, 1, 1);
        var audienceProfile = index == 0
            ? """
              {"spokenLanguages":[{"label":"English","sharePercent":80}],"understoodLanguages":[],"lifeStages":[{"label":"Business decision makers","sharePercent":60}],"lsmSemSegments":[{"label":"SEM 8-10","sharePercent":70}],"taxonomyName":"TGI SEM","taxonomyVersion":"2026","universe":"Johannesburg adults","measurementSource":"Fixture audience study","measurementPeriod":"2026 Q2","methodology":"Weighted aggregate survey","limitations":"Test fixture only","measurements":[{"metricType":"REACH","value":125000,"unit":"PEOPLE","universe":"Johannesburg adults","measurementSource":"Fixture audience study","measurementPeriod":"2026 Q2","methodology":"Weighted aggregate survey","limitations":"Test fixture only"}]}
              """
            : null;
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_products (
                id, tenant_id, supplier_id, supplier_product_code, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, $4, 'ACTIVE', 1, $5, $5)
            """, productId, TenantId, SupplierId, $"PLANNING-{index + 1}", Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code,
                product_type_code, geography, latitude, longitude, audience_profile_json,
                verification_code, deliverable_json, spatial_json,
                source_import_id, source_candidate_id, published_by, published_at_utc)
            VALUES ($1, $2, $3, 1, $4, 'OOH', 'OOH_SITE', $5, $6, $7, $8::jsonb,
                'HUMAN_VERIFIED',
                '{"format":"Static billboard","buyingUnit":"site/month","dimensions":"3m x 6m","placement":"Roadside","quantity":1}'::jsonb,
                '{"country":"South Africa","province":"Gauteng","municipality":"Johannesburg","locality":"Johannesburg","road":"Bree Street","trafficDirection":"Northbound","facingBearingDegrees":15,"pointsOfInterest":[{"name":"Central business district","category":"BUSINESS_DISTRICT","latitude":-26.2041,"longitude":28.0473}]}'::jsonb,
                $9, $10, $11, $12)
            """, versionId, TenantId, productId, $"{geography} Site {index + 1}", geography,
            index == 5 ? -33.9249m : -26.2041m,
            index == 5 ? 18.4241m : 28.0473m,
            audienceProfile is null ? DBNull.Value : audienceProfile,
            ImportId, CandidateId, OperatorId, Now);
        AddCommand(batch,
            "UPDATE commercial.inventory_products SET current_version_id = $1 WHERE id = $2",
            versionId, productId);
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator,
                vat_treatment_code, commercial_terms_json)
            VALUES ($1, $2, $3, 'MONTH_RATE', 'ZAR', $4, '2026-01-01', $5,
                'csv#row=2', 'INCLUSIVE',
                '{"vatTreatment":"INCLUSIVE","minimumOrder":1,"inclusions":["Media placement"],"exclusions":["Creative production"],"conditions":["Subject to written supplier confirmation"],"bookingLeadTimeDays":5}'::jsonb)
            """, rateId, TenantId, versionId, rate, effectiveTo);
        var availability = index is 0 or 1 ? "AVAILABLE" : "UNKNOWN";
        var validUntil = index == 0
            ? new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero)
            : index == 1
                ? new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero)
                : (DateTimeOffset?)null;
        var availabilitySource = index == 0
            ? "supplier-confirmation:email-001"
            : "supplier-rate-card";
        AddCommand(batch,
            """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code, observed_at_utc,
                valid_until_utc, source_locator)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            """, availabilityId, TenantId, versionId, availability, Now,
            validUntil.HasValue ? validUntil.Value : DBNull.Value, availabilitySource);
        await batch.ExecuteNonQueryAsync();
    }

    private static async Task SeedStructuredAudienceSetAsync(string connectionString)
    {
        var audienceSetId = Guid.Parse("7b000000-0000-0000-0000-000000000001");
        var definitionId = Guid.Parse("7b000000-0000-0000-0000-000000000002");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.audience_definition_sets (
                id, tenant_id, brief_version_id, version_no,
                target_audience_ids_json, targeting_rationale,
                positioning_statement, input_hash, agent_provider_code,
                agent_model_code, agent_incremental_cost_minor,
                status_code, created_by, created_at_utc)
            VALUES ($1, $2, $3, 2, jsonb_build_array($4::uuid),
                'Fixture target backed by a supplied aggregate study.',
                'Reach the approved structured target without individual inference.',
                repeat('c', 64), 'deterministic', 'fixture-v1', 0,
                'APPROVED', $5, $6)
            """, audienceSetId, TenantId, BriefVersionId, definitionId, OperatorId,
            Now.AddSeconds(1));
        AddCommand(batch,
            """
            INSERT INTO commercial.audience_definitions (
                id, tenant_id, audience_set_id, name, description, need_state,
                buying_context, geography_json, language, life_stage, lsm_sem,
                lsm_sem_taxonomy, lsm_sem_taxonomy_version, classification_code,
                exclusions_json, evidence_item_ids_json, confidence, status_code)
            VALUES ($1, $2, $3, 'Local business decision makers',
                'Aggregate audience described by the approved fixture evidence.',
                'Increase qualified enquiries', 'Business purchase decision',
                '["Johannesburg"]', 'English', 'Business decision makers', 'SEM 8-10',
                'TGI SEM', '2026', 'FACT', '[]', jsonb_build_array($4::uuid),
                0.9, 'APPROVED')
            """, definitionId, TenantId, audienceSetId, BriefSourceId);
        Assert.Equal(2, await batch.ExecuteNonQueryAsync());
    }

    private static void AddCommand(NpgsqlBatch batch, string sql, params object[] parameters)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in parameters)
        {
            command.Parameters.AddWithValue(value);
        }
        batch.BatchCommands.Add(command);
    }
}
