-- Local-only identities/workspace for connected browser certification of the real
-- buyer -> client -> supplier -> finance boundaries. This file is never used by
-- production Compose and contains no production credentials.

INSERT INTO commercial.tenants (
    id, type_code, legal_name, trading_name, slug, status_code,
    timezone, currency_code, vat_status_code, vat_number, settings_json,
    version, created_at_utc, updated_at_utc)
VALUES (
    '10000000-0000-0000-0000-000000000040', 'AGENCY',
    'Advertified Connected Buyer', 'Connected Buyer', 'connected-buyer-local',
    'ACTIVE', 'Africa/Johannesburg', 'ZAR', 'REGISTERED', NULL, '{}'::jsonb,
    1, clock_timestamp(), clock_timestamp())
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.users (
    id, email, display_name, phone, status_code, mfa_enabled,
    last_login_at_utc, version, created_at_utc, updated_at_utc)
VALUES
    ('10000000-0000-0000-0000-000000000041',
     'supplier.operator@advertified.local', 'Local Supplier Operator', NULL,
     'ACTIVE', false, NULL, 1, clock_timestamp(), clock_timestamp()),
    ('10000000-0000-0000-0000-000000000042',
     'finance.approver@advertified.local', 'Local Finance Approver', NULL,
     'ACTIVE', false, NULL, 1, clock_timestamp(), clock_timestamp()),
    ('10000000-0000-0000-0000-000000000050',
     'finance.reviewer@advertified.local', 'Local Finance Reviewer', NULL,
     'ACTIVE', false, NULL, 1, clock_timestamp(), clock_timestamp())
ON CONFLICT (id) DO NOTHING;

-- Buyer/operator membership in the isolated buyer workspace.
INSERT INTO commercial.memberships (
    id, tenant_id, user_id, role_code, status_code, invited_by,
    invited_at_utc, accepted_at_utc, version, created_at_utc, updated_at_utc)
VALUES
    ('10000000-0000-0000-0000-000000000043',
     '10000000-0000-0000-0000-000000000040',
     '10000000-0000-0000-0000-000000000001',
     'agency_admin', 'ACTIVE', NULL,
     clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()),
    ('10000000-0000-0000-0000-000000000044',
     '10000000-0000-0000-0000-000000000040',
     '10000000-0000-0000-0000-000000000004',
     'advertiser_approver', 'ACTIVE',
     '10000000-0000-0000-0000-000000000001',
     clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()),
    ('10000000-0000-0000-0000-000000000045',
     '10000000-0000-0000-0000-000000000040',
     '10000000-0000-0000-0000-000000000042',
     'platform_admin', 'ACTIVE',
     '10000000-0000-0000-0000-000000000001',
     clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()),
    ('10000000-0000-0000-0000-000000000051',
     '10000000-0000-0000-0000-000000000040',
     '10000000-0000-0000-0000-000000000050',
     'platform_admin', 'ACTIVE',
     '10000000-0000-0000-0000-000000000001',
     clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp()),
    -- The existing Advertified Local workspace owns the published local inventory.
    -- This separate supplier identity exercises rfq_respond / booking_confirm rather
    -- than letting the buyer impersonate a supplier action.
    ('10000000-0000-0000-0000-000000000046',
     '10000000-0000-0000-0000-000000000002',
     '10000000-0000-0000-0000-000000000041',
     'supplier_user', 'ACTIVE',
     '10000000-0000-0000-0000-000000000001',
     clock_timestamp(), clock_timestamp(), 1, clock_timestamp(), clock_timestamp())
ON CONFLICT (id) DO NOTHING;

-- Supplier-role tenant membership is intentionally not sufficient on its own. Bind the
-- acceptance supplier user to the exact Local Demo Media Owner inventory supplier so the
-- real supplier-scope policy remains enforced during connected browser certification.
INSERT INTO commercial.inventory_supplier_memberships (
    id, tenant_id, supplier_id, user_id, role_code, status_code,
    created_by, accepted_at_utc, version, created_at_utc, updated_at_utc)
VALUES (
    '10000000-0000-0000-0000-000000000049',
    '10000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000100',
    '10000000-0000-0000-0000-000000000041',
    'supplier_user', 'ACTIVE',
    '10000000-0000-0000-0000-000000000001',
    clock_timestamp(), 1, clock_timestamp(), clock_timestamp())
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.commercial_policies (
    id, tenant_id, current_version_id, version, created_at_utc, updated_at_utc)
VALUES (
    '10000000-0000-0000-0000-000000000047',
    '10000000-0000-0000-0000-000000000040',
    NULL, 1, clock_timestamp(), clock_timestamp())
ON CONFLICT (tenant_id) DO NOTHING;

INSERT INTO commercial.commercial_policy_versions (
    id, tenant_id, policy_id, version_number,
    markup_basis_points, management_fee_basis_points, commission_basis_points,
    vat_status_code, vat_rate_basis_points, prices_include_vat,
    currency_code, booking_approval_threshold_minor, allow_self_approval,
    created_by, created_at_utc)
SELECT
    '10000000-0000-0000-0000-000000000048',
    '10000000-0000-0000-0000-000000000040',
    policy.id, 1, 1000, 0, 500, 'REGISTERED', 1500, false,
    'ZAR', 100000000, true,
    '10000000-0000-0000-0000-000000000001', clock_timestamp()
FROM commercial.commercial_policies policy
WHERE policy.tenant_id = '10000000-0000-0000-0000-000000000040'
  AND policy.current_version_id IS NULL
ON CONFLICT (id) DO NOTHING;

UPDATE commercial.commercial_policies
SET current_version_id = '10000000-0000-0000-0000-000000000048',
    updated_at_utc = clock_timestamp()
WHERE tenant_id = '10000000-0000-0000-0000-000000000040'
  AND current_version_id IS NULL;

-- Local-only repair for campaigns completed before materialised proof requests were
-- introduced. Production campaigns create these rows atomically inside completion.
INSERT INTO commercial.delivery_proof_requests (
    buyer_tenant_id, supplier_tenant_id, campaign_id, booking_id,
    supplier_name, product_name, channel_code, geography,
    flight_start, flight_end, campaign_owner_user_id, opportunity_id,
    proof_requested_by, proof_requested_at_utc, proof_request_reason)
SELECT booking.buyer_tenant_id, booking.supplier_tenant_id,
    campaign.id, booking.id, booking.supplier_name, booking.product_name,
    booking.channel_code, booking.geography, booking.flight_start,
    booking.flight_end, campaign.owner_user_id, brief.opportunity_id,
    campaign.proof_requested_by, campaign.proof_requested_at_utc,
    campaign.proof_request_reason
FROM commercial.bookings booking
JOIN commercial.campaigns campaign
  ON campaign.tenant_id = booking.buyer_tenant_id
 AND campaign.proposal_decision_id = booking.proposal_decision_id
 AND campaign.plan_version_id = booking.plan_version_id
JOIN commercial.campaign_briefs brief
  ON brief.tenant_id = campaign.tenant_id
 AND brief.id = campaign.brief_id
WHERE booking.status_code = 'CONFIRMED'
  AND campaign.status_code = 'COMPLETED'
  AND campaign.proof_requested_by IS NOT NULL
  AND campaign.proof_requested_at_utc IS NOT NULL
  AND btrim(campaign.proof_request_reason) <> ''
ON CONFLICT (buyer_tenant_id, campaign_id, booking_id) DO UPDATE
SET campaign_owner_user_id = EXCLUDED.campaign_owner_user_id,
    opportunity_id = EXCLUDED.opportunity_id;
