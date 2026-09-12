-- Optional local-development inventory bootstrap.
-- This is intentionally separate from the canonical workspace seed so stale or experimental
-- inventory fixtures cannot prevent the Commercial API from starting.
\set ON_ERROR_STOP on
\ir inventory-bootstrap.generated.sql
