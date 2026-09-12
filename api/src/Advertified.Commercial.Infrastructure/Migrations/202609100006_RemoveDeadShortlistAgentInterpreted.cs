using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609100006_RemoveDeadShortlistAgentInterpreted")]
public sealed class RemoveDeadShortlistAgentInterpreted : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION commercial.read_inventory_decisions(
            uuid, uuid, timestamptz, uuid, uuid);

        ALTER TABLE commercial.inventory_shortlist_candidates
            DROP COLUMN agent_interpreted;

        CREATE FUNCTION commercial.read_inventory_decisions(
            p_brief uuid, p_product uuid, p_before timestamptz,
            p_before_event uuid, p_before_product uuid)
        RETURNS TABLE (
            "EventId" uuid, "PreviousEventId" uuid, "ProductId" uuid,
            "ProductVersionId" uuid, "PreviousProductVersionId" uuid, "ProductName" text,
            "IsSelected" boolean, "WasSelected" boolean, "PresentInCurrentShortlist" boolean,
            "DecidedAtUtc" timestamptz,
            "ActorId" uuid, "Reason" text, "BriefVersionId" uuid, "ShortlistVersionId" uuid)
        LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = pg_catalog AS $function$
        DECLARE caller_tenant uuid := commercial.current_tenant_id();
            caller_user uuid := commercial.current_user_id();
            caller_role text;
        BEGIN
            IF (p_brief IS NULL) = (p_product IS NULL) THEN
                RAISE EXCEPTION 'Report scope required' USING ERRCODE = '42501';
            END IF;
            IF p_before IS NOT NULL AND (p_before_event IS NULL OR p_before_product IS NULL) THEN
                RAISE EXCEPTION 'Report cursor invalid' USING ERRCODE = '22023';
            END IF;
            SELECT role_code INTO caller_role FROM commercial.memberships
              WHERE tenant_id = caller_tenant AND user_id = caller_user AND status_code = 'ACTIVE';
            IF caller_role IS NULL THEN
                RAISE EXCEPTION 'Report access denied' USING ERRCODE = '42501';
            END IF;
            IF p_brief IS NOT NULL AND (caller_role NOT IN (
                'platform_admin', 'internal_planner', 'agency_admin', 'agency_campaign_user') OR
                NOT EXISTS (SELECT 1 FROM commercial.brief_versions v
                    JOIN commercial.campaign_briefs b ON b.tenant_id = v.tenant_id AND b.id = v.brief_id
                    WHERE v.tenant_id = caller_tenant AND v.id = p_brief AND b.owner_user_id = caller_user)) THEN
                RAISE EXCEPTION 'Planning assignment denied' USING ERRCODE = '42501';
            END IF;
            IF p_product IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM commercial.inventory_products product
                WHERE product.tenant_id = caller_tenant AND product.id = p_product
                  AND (caller_role IN ('platform_admin', 'inventory_ops') OR EXISTS (
                    SELECT 1 FROM commercial.inventory_supplier_memberships membership
                    WHERE membership.tenant_id = caller_tenant AND membership.user_id = caller_user
                      AND membership.supplier_id = product.supplier_id AND membership.status_code = 'ACTIVE'
                      AND membership.role_code = caller_role
                      AND caller_role IN ('supplier_user', 'influencer_rep')))) THEN
                RAISE EXCEPTION 'Supplier inventory access denied' USING ERRCODE = '42501';
            END IF;
            RETURN QUERY
            WITH campaign_scope AS (
                SELECT DISTINCT b.tenant_id, b.brief_id FROM commercial.brief_versions b
                WHERE (p_brief IS NOT NULL AND b.tenant_id = caller_tenant AND b.id = p_brief)
                   OR (p_product IS NOT NULL AND EXISTS (
                     SELECT 1 FROM commercial.inventory_shortlist_versions h
                     JOIN commercial.inventory_shortlist_candidates c
                       ON c.tenant_id = h.tenant_id AND c.shortlist_version_id = h.id
                     WHERE h.tenant_id = b.tenant_id AND h.brief_version_id = b.id
                       AND c.inventory_tenant_id = caller_tenant AND c.inventory_product_id = p_product))
            ), approved AS (
                SELECT h.*, b.brief_id, b.version_no AS brief_sequence, checkpoint.id AS decision_id,
                    checkpoint.selected_by, checkpoint.selected_at_utc, checkpoint.reason
                FROM commercial.inventory_shortlist_versions h
                JOIN commercial.brief_versions b ON b.tenant_id = h.tenant_id AND b.id = h.brief_version_id
                JOIN campaign_scope scope ON scope.tenant_id = b.tenant_id AND scope.brief_id = b.brief_id
                JOIN LATERAL (
                    SELECT s.* FROM commercial.shortlist_selections s
                    JOIN commercial.inventory_shortlist_candidates c
                      ON c.tenant_id = s.tenant_id AND c.id = s.shortlist_candidate_id
                    WHERE c.tenant_id = h.tenant_id AND c.shortlist_version_id = h.id
                    ORDER BY s.selected_at_utc, s.id LIMIT 1
                ) checkpoint ON true
            ), pairs AS (
                SELECT current.*, previous.id AS previous_shortlist_id
                FROM approved current LEFT JOIN LATERAL (
                    SELECT prior.id FROM approved prior
                    WHERE prior.tenant_id = current.tenant_id AND prior.brief_id = current.brief_id
                      AND prior.mix_version_id = current.mix_version_id
                      AND (prior.selected_at_utc, prior.brief_sequence, prior.version_no, prior.id)
                        < (current.selected_at_utc, current.brief_sequence, current.version_no, current.id)
                    ORDER BY prior.selected_at_utc DESC, prior.brief_sequence DESC, prior.version_no DESC, prior.id DESC LIMIT 1
                ) previous ON true
            ), decisions AS (
                SELECT pair.*, item.inventory_tenant_id, item.inventory_product_id,
                    item.product_version_id, item.product_name,
                    item.selection_id, item.is_selected, item.previous_event,
                    item.previous_product_version, item.was_selected, item.present
                FROM pairs pair CROSS JOIN LATERAL (
                    SELECT COALESCE(c.inventory_tenant_id, old.inventory_tenant_id) AS inventory_tenant_id,
                        COALESCE(c.inventory_product_id, old.inventory_product_id) AS inventory_product_id,
                        COALESCE(c.product_version_id, old.product_version_id) AS product_version_id,
                        COALESCE(c.product_name, old.product_name) AS product_name,
                        c.selection_id, COALESCE(c.is_selected, false) AS is_selected,
                        old.selection_id AS previous_event, old.product_version_id AS previous_product_version,
                        old.is_selected AS was_selected, c.id IS NOT NULL AS present
                    FROM (
                        SELECT candidate.*, selection.id AS selection_id, selection.is_selected
                        FROM commercial.inventory_shortlist_candidates candidate
                        LEFT JOIN commercial.shortlist_selections selection
                          ON selection.tenant_id = candidate.tenant_id AND selection.shortlist_candidate_id = candidate.id
                        WHERE candidate.tenant_id = pair.tenant_id AND candidate.shortlist_version_id = pair.id
                    ) c FULL JOIN (
                        SELECT candidate.*, selection.id AS selection_id, selection.is_selected
                        FROM commercial.inventory_shortlist_candidates candidate
                        JOIN commercial.shortlist_selections selection
                          ON selection.tenant_id = candidate.tenant_id AND selection.shortlist_candidate_id = candidate.id
                        WHERE candidate.tenant_id = pair.tenant_id
                          AND candidate.shortlist_version_id = pair.previous_shortlist_id
                    ) old ON old.inventory_tenant_id = c.inventory_tenant_id
                         AND old.inventory_product_id = c.inventory_product_id
                    WHERE c.is_eligible OR old.is_selected
                ) item
                WHERE p_product IS NULL OR (item.inventory_tenant_id = caller_tenant AND item.inventory_product_id = p_product)
            )
            SELECT COALESCE(d.selection_id, d.decision_id), d.previous_event,
                d.inventory_product_id, d.product_version_id, d.previous_product_version,
                d.product_name::text, d.is_selected, d.was_selected, d.present,
                d.selected_at_utc,
                CASE WHEN p_product IS NULL THEN d.selected_by END,
                CASE WHEN p_product IS NULL THEN d.reason::text END,
                CASE WHEN p_product IS NULL THEN d.brief_version_id END,
                CASE WHEN p_product IS NULL THEN d.id END
            FROM decisions d
            WHERE p_before IS NULL OR
                (d.selected_at_utc, COALESCE(d.selection_id, d.decision_id), d.inventory_product_id)
                  < (p_before, p_before_event, p_before_product)
            ORDER BY d.selected_at_utc DESC,
                COALESCE(d.selection_id, d.decision_id) DESC,
                d.inventory_product_id DESC
            LIMIT 101;
        END $function$;
        REVOKE ALL ON FUNCTION commercial.read_inventory_decisions(
            uuid, uuid, timestamptz, uuid, uuid) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.read_inventory_decisions(
            uuid, uuid, timestamptz, uuid, uuid) TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Removing dead shortlist agent metadata is forward-only.");
}
