-- Gate 0 database bootstrap only.
-- Application schemas, tables, reference data, and users belong to reviewed migrations.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS vector;

DO $verification$
DECLARE
    required_extension_count integer;
BEGIN
    SELECT COUNT(*)
    INTO required_extension_count
    FROM pg_extension
    WHERE extname IN ('pgcrypto', 'postgis', 'vector');

    IF required_extension_count <> 3 THEN
        RAISE EXCEPTION 'Required PostgreSQL extensions are not all installed';
    END IF;
END
$verification$;
