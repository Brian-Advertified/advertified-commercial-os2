-- Optional local-development Marketplace projection.
-- Run only after the core workspace and local inventory bootstrap have completed.
\set ON_ERROR_STOP on
\ir publish-current-inventory-to-marketplace.sql
