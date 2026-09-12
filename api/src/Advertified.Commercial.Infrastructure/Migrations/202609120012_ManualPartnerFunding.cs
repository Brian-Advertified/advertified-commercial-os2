using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609120012_ManualPartnerFunding")]
public sealed partial class ManualPartnerFunding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE governance.master_data_collections
        SET registry_version = '2.43.0', updated_at_utc = now()
        WHERE code = 'paymentMethods';
        UPDATE governance.master_data_items
        SET is_active = true,
            metadata_json = '{"activationRequiresProviderIntegration":false,"mode":"MANUAL_PARTNER_REFERRAL","reconciliationEvidence":"PARTNER_EMAIL","independentHumanReconciliationRequired":true}'::jsonb,
            updated_at_utc = now()
        WHERE collection_code = 'paymentMethods' AND code = 'ADVERTISE_NOW_PAY_LATER';
        """ + "\n" + PaymentTransitionSql(includeManualPartner: true));

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE governance.master_data_items
        SET is_active = false,
            metadata_json = '{"activationRequiresProviderIntegration":true,"mode":"REFERRAL"}'::jsonb,
            updated_at_utc = now()
        WHERE collection_code = 'paymentMethods' AND code = 'ADVERTISE_NOW_PAY_LATER';
        -- Retain payment records and all reference-data audit history.
        """ + "\n" + PaymentTransitionSql(includeManualPartner: false));
}
