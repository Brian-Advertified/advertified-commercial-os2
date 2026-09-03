using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryExtractionDurabilityHardening
{
    private static void AddFailureStateConstraint(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE commercial.inventory_extraction_attempts
                ADD CONSTRAINT ck_inventory_extraction_failure_state CHECK (
                    (status_code IN (
                        'FAILED_RETRYABLE', 'FAILED_TERMINAL', 'TIMED_OUT',
                        'RECONCILIATION_REQUIRED', 'CANCELLED') AND
                     failure_class_code IS NOT NULL) OR
                    (status_code NOT IN (
                        'FAILED_RETRYABLE', 'FAILED_TERMINAL', 'TIMED_OUT',
                        'RECONCILIATION_REQUIRED', 'CANCELLED') AND
                     failure_class_code IS NULL));
            """);
}
