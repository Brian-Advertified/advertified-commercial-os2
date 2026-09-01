-- Deterministic local-development identity and workspace.
-- This file is run only by docker-compose.app.yml after governed migrations complete.

INSERT INTO commercial.tenants (
    id, type_code, legal_name, trading_name, slug, status_code,
    timezone, currency_code, vat_status_code, vat_number, settings_json,
    version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000002', 'AGENCY',
    'Advertified Local Agency', 'Advertified Local', 'advertified-local',
    'ACTIVE', 'Africa/Johannesburg', 'ZAR', 'REGISTERED', NULL, '{}'::jsonb,
    1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.users (
    id, email, display_name, phone, status_code, mfa_enabled,
    last_login_at_utc, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000001',
    'developer@advertified.local', 'Local Planner', NULL, 'ACTIVE', false,
    clock_timestamp(), 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.memberships (
    id, tenant_id, user_id, role_code, status_code, invited_by,
    invited_at_utc, accepted_at_utc, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000003',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000001',
    'agency_admin', 'ACTIVE', NULL,
    clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

-- Development-only client approver. This preserves the production proposal sharing boundary
-- without weakening the agency operator's permissions.
INSERT INTO commercial.users (
    id, email, display_name, phone, status_code, mfa_enabled,
    last_login_at_utc, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000004',
    'client.approver@advertified.local', 'Local Client Approver', NULL, 'ACTIVE', false,
    NULL, 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.memberships (
    id, tenant_id, user_id, role_code, status_code, invited_by,
    invited_at_utc, accepted_at_utc, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000005',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000004',
    'advertiser_approver', 'ACTIVE', '10000000-0000-0000-0000-000000000001',
    clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

-- Governed local proposal prerequisite. These records are deliberately named Local Demo,
-- remain confined to the development Compose database, and retain supplier/import/candidate
-- lineage so they cannot be confused with live supplier inventory.
INSERT INTO commercial.inventory_suppliers (
    id, tenant_id, name, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000100',
    '10000000-0000-0000-0000-000000000002',
    'Local Demo Media Owner', 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_imports (
    id, tenant_id, supplier_id, source_file_name, declared_media_type,
    document_class_collection_code, document_class_code, status_code,
    scan_status_code, quarantine_object_key, protected_object_key,
    source_hash, source_size, created_by, version, created_at_utc, updated_at_utc
)
VALUES (
    '10000000-0000-0000-0000-000000000101',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000100',
    'local-demo-proposal-inventory.csv', 'text/csv',
    'documentClasses', 'CSV', 'COMPLETED', 'CLEAN',
    'development/local-demo-proposal-inventory.csv',
    'development/local-demo-proposal-inventory.csv',
    repeat('d', 64), 512,
    '10000000-0000-0000-0000-000000000001',
    2, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_candidates (
    id, tenant_id, import_id, row_number, status_code,
    proposed_values_json, canonical_values_json, validation_json,
    source_locator, reviewed_by, version, created_at_utc, updated_at_utc
)
VALUES
(
    '10000000-0000-0000-0000-000000000102',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000101',
    2, 'APPROVED', '{}'::jsonb, '{}'::jsonb, '[]'::jsonb,
    'local-demo-proposal-inventory.csv#row=2',
    '10000000-0000-0000-0000-000000000001',
    1, clock_timestamp(), clock_timestamp()
),
(
    '10000000-0000-0000-0000-000000000103',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000101',
    3, 'APPROVED', '{}'::jsonb, '{}'::jsonb, '[]'::jsonb,
    'local-demo-proposal-inventory.csv#row=3',
    '10000000-0000-0000-0000-000000000001',
    1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_products (
    id, tenant_id, supplier_id, supplier_product_code, status_code,
    version, created_at_utc, updated_at_utc
)
VALUES
(
    '10000000-0000-0000-0000-000000000110',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000100',
    'LOCAL-DEMO-JHB-001', 'ACTIVE', 1, clock_timestamp(), clock_timestamp()
),
(
    '10000000-0000-0000-0000-000000000111',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000100',
    'LOCAL-DEMO-JHB-002', 'ACTIVE', 1, clock_timestamp(), clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_product_versions (
    id, tenant_id, product_id, version_number, name, channel_code,
    product_type_code, geography, latitude, longitude, verification_code,
    source_import_id, source_candidate_id, published_by, published_at_utc
)
VALUES
(
    '10000000-0000-0000-0000-000000000120',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000110',
    1, 'Local Demo Johannesburg Digital Billboard', 'OOH', 'OOH_SITE',
    'Johannesburg', -26.2041, 28.0473, 'HUMAN_VERIFIED',
    '10000000-0000-0000-0000-000000000101',
    '10000000-0000-0000-0000-000000000102',
    '10000000-0000-0000-0000-000000000001', clock_timestamp()
),
(
    '10000000-0000-0000-0000-000000000121',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000111',
    1, 'Local Demo Johannesburg Roadside Billboard', 'OOH', 'OOH_SITE',
    'Johannesburg', -26.1076, 28.0567, 'HUMAN_VERIFIED',
    '10000000-0000-0000-0000-000000000101',
    '10000000-0000-0000-0000-000000000103',
    '10000000-0000-0000-0000-000000000001', clock_timestamp()
)
ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_products
SET current_version_id = CASE id
    WHEN '10000000-0000-0000-0000-000000000110'::uuid
        THEN '10000000-0000-0000-0000-000000000120'::uuid
    WHEN '10000000-0000-0000-0000-000000000111'::uuid
        THEN '10000000-0000-0000-0000-000000000121'::uuid
END
WHERE id IN (
    '10000000-0000-0000-0000-000000000110'::uuid,
    '10000000-0000-0000-0000-000000000111'::uuid
);

INSERT INTO commercial.inventory_rates (
    id, tenant_id, product_version_id, rate_type_code, currency_code,
    amount_minor, effective_from, effective_to, source_locator
)
VALUES
(
    '10000000-0000-0000-0000-000000000130',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000120',
    'MONTH_RATE', 'ZAR', 2500000, '2026-01-01', '2027-12-31',
    'local-demo-proposal-inventory.csv#row=2'
),
(
    '10000000-0000-0000-0000-000000000131',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000121',
    'MONTH_RATE', 'ZAR', 3200000, '2026-01-01', '2027-12-31',
    'local-demo-proposal-inventory.csv#row=3'
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_availability (
    id, tenant_id, product_version_id, availability_code,
    observed_at_utc, valid_until_utc, source_locator
)
VALUES
(
    '10000000-0000-0000-0000-000000000140',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000120',
    'AVAILABLE', clock_timestamp(), '2027-12-31T23:59:59Z',
    'development-confirmation:local-demo-jhb-001'
),
(
    '10000000-0000-0000-0000-000000000141',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000121',
    'AVAILABLE', clock_timestamp(), '2027-12-31T23:59:59Z',
    'development-confirmation:local-demo-jhb-002'
)
ON CONFLICT (id) DO NOTHING;
