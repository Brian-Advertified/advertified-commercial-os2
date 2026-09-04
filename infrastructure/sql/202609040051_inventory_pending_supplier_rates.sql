BEGIN;

DO $migration$
DECLARE
    column_record record;
BEGIN
    FOR column_record IN
        SELECT table_schema, table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'commercial'
          AND table_name LIKE '%inventory%rate%'
          AND column_name IN (
              'rate_type_code',
              'currency_code',
              'amount_minor',
              'rate_amount_minor'
          )
          AND is_nullable = 'NO'
    LOOP
        EXECUTE format(
            'ALTER TABLE %I.%I ALTER COLUMN %I DROP NOT NULL',
            column_record.table_schema,
            column_record.table_name,
            column_record.column_name
        );
    END LOOP;
END
$migration$;

COMMENT ON SCHEMA commercial IS
    'Advertified commercial schema. Inventory products may retain pending supplier pricing without invented zero amounts or buying bases.';

COMMIT;
