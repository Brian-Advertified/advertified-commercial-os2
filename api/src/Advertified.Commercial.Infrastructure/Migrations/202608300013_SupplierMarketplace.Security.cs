using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class SupplierMarketplace
{
    private static void CreateMarketplaceSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.marketplace_listings ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.marketplace_listings FORCE ROW LEVEL SECURITY;
            CREATE POLICY marketplace_listings_read ON commercial.marketplace_listings
                FOR SELECT USING (
                    supplier_tenant_id = commercial.current_tenant_id()
                    OR (status_code = 'PUBLISHED' AND EXISTS (
                        SELECT 1 FROM commercial.memberships membership
                        WHERE membership.tenant_id = commercial.current_tenant_id()
                          AND membership.user_id = commercial.current_user_id()
                          AND membership.status_code = 'ACTIVE')));
            CREATE POLICY marketplace_listings_write ON commercial.marketplace_listings
                FOR ALL USING (supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (supplier_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.marketplace_listing_versions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.marketplace_listing_versions FORCE ROW LEVEL SECURITY;
            CREATE POLICY marketplace_listing_versions_read
                ON commercial.marketplace_listing_versions FOR SELECT USING (
                    supplier_tenant_id = commercial.current_tenant_id()
                    OR EXISTS (
                        SELECT 1 FROM commercial.marketplace_listings listing
                        WHERE listing.supplier_tenant_id = marketplace_listing_versions.supplier_tenant_id
                          AND listing.id = marketplace_listing_versions.listing_id
                          AND listing.status_code = 'PUBLISHED'
                          AND listing.current_version_id = marketplace_listing_versions.id
                          AND EXISTS (
                              SELECT 1 FROM commercial.memberships membership
                              WHERE membership.tenant_id = commercial.current_tenant_id()
                                AND membership.user_id = commercial.current_user_id()
                                AND membership.status_code = 'ACTIVE'))
                    OR EXISTS (
                        SELECT 1 FROM commercial.marketplace_rfqs rfq
                        WHERE rfq.listing_version_id = marketplace_listing_versions.id
                          AND (rfq.buyer_tenant_id = commercial.current_tenant_id()
                            OR rfq.supplier_tenant_id = commercial.current_tenant_id())));
            CREATE POLICY marketplace_listing_versions_write
                ON commercial.marketplace_listing_versions FOR INSERT
                WITH CHECK (supplier_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.marketplace_rfqs ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.marketplace_rfqs FORCE ROW LEVEL SECURITY;
            CREATE POLICY marketplace_rfqs_read ON commercial.marketplace_rfqs
                FOR SELECT USING (
                    buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());
            CREATE POLICY marketplace_rfqs_insert ON commercial.marketplace_rfqs
                FOR INSERT WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());
            CREATE POLICY marketplace_rfqs_update ON commercial.marketplace_rfqs
                FOR UPDATE USING (buyer_tenant_id = commercial.current_tenant_id())
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.marketplace_supplier_responses ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.marketplace_supplier_responses FORCE ROW LEVEL SECURITY;
            CREATE POLICY marketplace_responses_read ON commercial.marketplace_supplier_responses
                FOR SELECT USING (
                    buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());
            CREATE POLICY marketplace_responses_insert ON commercial.marketplace_supplier_responses
                FOR INSERT WITH CHECK (supplier_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.marketplace_response_acceptances ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.marketplace_response_acceptances FORCE ROW LEVEL SECURITY;
            CREATE POLICY marketplace_acceptances_read ON commercial.marketplace_response_acceptances
                FOR SELECT USING (
                    buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());
            CREATE POLICY marketplace_acceptances_insert ON commercial.marketplace_response_acceptances
                FOR INSERT WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            CREATE TRIGGER protect_marketplace_listing_versions
                BEFORE UPDATE OR DELETE ON commercial.marketplace_listing_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_marketplace_supplier_responses
                BEFORE UPDATE OR DELETE ON commercial.marketplace_supplier_responses
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_marketplace_response_acceptances
                BEFORE UPDATE OR DELETE ON commercial.marketplace_response_acceptances
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            GRANT SELECT, INSERT, UPDATE ON commercial.marketplace_listings
                TO advertified_app;
            GRANT SELECT, INSERT ON commercial.marketplace_listing_versions
                TO advertified_app;
            GRANT SELECT, INSERT, UPDATE ON commercial.marketplace_rfqs
                TO advertified_app;
            GRANT SELECT, INSERT ON commercial.marketplace_supplier_responses
                TO advertified_app;
            GRANT SELECT, INSERT ON commercial.marketplace_response_acceptances
                TO advertified_app;
            """);
    }
}
