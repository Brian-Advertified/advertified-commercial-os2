\set ON_ERROR_STOP on
BEGIN;
SET LOCAL app.current_tenant_id = '10000000-0000-0000-0000-000000000002';
SET LOCAL app.current_actor_id = '10000000-0000-0000-0000-000000000001';

-- Project current, reviewed inventory into buyer-visible Marketplace listings.
-- This seed owns its transaction and explicit local tenant/actor session.
INSERT INTO commercial.marketplace_listings (
    id, supplier_tenant_id, product_id, status_code, terms,
    created_by, version, created_at_utc, updated_at_utc)
SELECT gen_random_uuid(), product.tenant_id, product.id, 'DRAFT',
    'Subject to final human-approved booking and current availability.',
    current_setting('app.current_actor_id')::uuid, 1,
    clock_timestamp(), clock_timestamp()
FROM commercial.inventory_products product
JOIN commercial.inventory_product_versions product_version
  ON product_version.tenant_id = product.tenant_id
 AND product_version.id = product.current_version_id
WHERE product.tenant_id = current_setting('app.current_tenant_id')::uuid
  AND product.status_code = 'ACTIVE'
  AND product_version.published_at_utc IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM commercial.inventory_product_identity_links identity_link
      WHERE identity_link.tenant_id = product.tenant_id
        AND identity_link.duplicate_product_id = product.id)
  AND EXISTS (
      SELECT 1
      FROM commercial.inventory_rates rate
      WHERE rate.tenant_id = product_version.tenant_id
        AND rate.product_version_id = product_version.id
        AND (rate.effective_from IS NULL OR rate.effective_from <= CURRENT_DATE)
        AND (rate.effective_to IS NULL OR rate.effective_to >= CURRENT_DATE))
  AND (
      SELECT availability.availability_code <> 'UNAVAILABLE'
        AND (availability.valid_until_utc IS NULL
          OR availability.valid_until_utc >= clock_timestamp())
      FROM commercial.inventory_availability availability
      WHERE availability.tenant_id = product_version.tenant_id
        AND availability.product_version_id = product_version.id
        AND (availability.observed_at_utc IS NULL
          OR availability.observed_at_utc <= clock_timestamp())
      ORDER BY availability.observed_at_utc DESC NULLS LAST, availability.id DESC
      LIMIT 1) IS TRUE
ON CONFLICT (supplier_tenant_id, product_id) DO NOTHING;

CREATE TEMP TABLE inventory_marketplace_seed_eligible ON COMMIT DROP AS
SELECT listing.id AS listing_id,
    product.id AS product_id,
    product_version.id AS product_version_id,
    product.supplier_id AS supplier_id,
    rate.id AS rate_id,
    availability.id AS availability_id,
    supplier.name AS supplier_name,
    product_version.name AS product_name,
    product_version.channel_code AS channel_code,
    product_version.product_type_code AS product_type_code,
    product_version.geography,
    product_version.audience_profile_json,
    rate.rate_type_code,
    rate.amount_minor,
    rate.currency_code,
    availability.availability_code,
    rate.effective_from AS rate_effective_from,
    rate.effective_to AS rate_effective_to,
    rate.source_locator AS rate_source_locator,
    availability.source_locator AS availability_source_locator,
    availability.observed_at_utc AS availability_observed_at_utc,
    availability.valid_until_utc AS availability_valid_until_utc,
    supplier_version.vat_status_code AS supplier_vat_status_code,
    CASE WHEN supplier_version.id IS NULL THEN NULL ELSE jsonb_build_object(
        'vatStatus', supplier_version.vat_status_code,
        'vatNumber', supplier_version.vat_number,
        'commissionTerms', supplier_version.commission_terms,
        'paymentTerms', supplier_version.payment_terms,
        'cancellationTerms', supplier_version.cancellation_terms,
        'bookingDeadlineTerms', supplier_version.booking_deadline_terms)
    END AS supplier_commercial_json,
    rate.vat_treatment_code,
    rate.commercial_terms_json,
    product_version.deliverable_json,
    product_version.spatial_json,
    product_version.spatial_location AS private_spatial_location,
    product_version.coverage_geometry AS private_coverage_geometry,
    product_version.catchment_geometry AS private_catchment_geometry,
    product_version.route_geometry AS private_route_geometry,
    listing.terms
FROM commercial.marketplace_listings listing
JOIN commercial.inventory_products product
  ON product.tenant_id = listing.supplier_tenant_id
 AND product.id = listing.product_id
JOIN commercial.inventory_suppliers supplier
  ON supplier.tenant_id = product.tenant_id
 AND supplier.id = product.supplier_id
LEFT JOIN commercial.inventory_supplier_versions supplier_version
  ON supplier_version.tenant_id = supplier.tenant_id
 AND supplier_version.id = supplier.current_commercial_version_id
JOIN commercial.inventory_product_versions product_version
  ON product_version.tenant_id = product.tenant_id
 AND product_version.id = product.current_version_id
JOIN LATERAL (
    SELECT item.*
    FROM commercial.inventory_rates item
    WHERE item.tenant_id = product_version.tenant_id
      AND item.product_version_id = product_version.id
      AND (item.effective_from IS NULL OR item.effective_from <= CURRENT_DATE)
      AND (item.effective_to IS NULL OR item.effective_to >= CURRENT_DATE)
    ORDER BY item.effective_from DESC NULLS LAST, item.id DESC
    LIMIT 1) rate ON TRUE
JOIN LATERAL (
    SELECT item.*
    FROM commercial.inventory_availability item
    WHERE item.tenant_id = product_version.tenant_id
      AND item.product_version_id = product_version.id
      AND (item.observed_at_utc IS NULL OR item.observed_at_utc <= clock_timestamp())
    ORDER BY item.observed_at_utc DESC NULLS LAST, item.id DESC
    LIMIT 1) availability ON TRUE
WHERE listing.supplier_tenant_id = current_setting('app.current_tenant_id')::uuid
  AND listing.created_by = current_setting('app.current_actor_id')::uuid
  AND listing.status_code <> 'ARCHIVED'
  AND product.status_code = 'ACTIVE'
  AND product_version.published_at_utc IS NOT NULL
  AND availability.availability_code <> 'UNAVAILABLE'
  AND (availability.valid_until_utc IS NULL
    OR availability.valid_until_utc >= clock_timestamp())
  AND NOT EXISTS (
      SELECT 1
      FROM commercial.inventory_product_identity_links identity_link
      WHERE identity_link.tenant_id = product.tenant_id
        AND identity_link.duplicate_product_id = product.id);

CREATE TEMP TABLE inventory_marketplace_seed_pending ON COMMIT DROP AS
SELECT eligible.*,
    gen_random_uuid() AS listing_version_id,
    COALESCE((
        SELECT max(existing.version_number) + 1
        FROM commercial.marketplace_listing_versions existing
        WHERE existing.supplier_tenant_id =
              current_setting('app.current_tenant_id')::uuid
          AND existing.listing_id = eligible.listing_id), 1) AS listing_version_number
FROM inventory_marketplace_seed_eligible eligible
JOIN commercial.marketplace_listings listing
  ON listing.supplier_tenant_id = current_setting('app.current_tenant_id')::uuid
 AND listing.id = eligible.listing_id
LEFT JOIN commercial.marketplace_listing_versions current_version
  ON current_version.supplier_tenant_id = listing.supplier_tenant_id
 AND current_version.id = listing.current_version_id
WHERE current_version.id IS NULL
   OR current_version.product_version_id IS DISTINCT FROM eligible.product_version_id
   OR current_version.rate_id IS DISTINCT FROM eligible.rate_id
   OR current_version.availability_id IS DISTINCT FROM eligible.availability_id;

INSERT INTO commercial.marketplace_listing_versions (
    id, supplier_tenant_id, listing_id, version_number,
    product_version_id, supplier_id, rate_id, availability_id,
    supplier_name, product_name, channel_code, product_type_code, geography,
    audience_profile_json, rate_type_code, amount_minor, currency_code,
    availability_code, rate_effective_from, rate_effective_to,
    rate_source_locator, availability_source_locator,
    availability_observed_at_utc, availability_valid_until_utc,
    supplier_vat_status_code, vat_treatment_code,
    supplier_commercial_json, commercial_terms_json,
    deliverable_json, spatial_json, logo_asset_id,
    private_spatial_location, private_coverage_geometry,
    private_catchment_geometry, private_route_geometry,
    terms, published_by, published_at_utc)
SELECT listing_version_id,
    current_setting('app.current_tenant_id')::uuid,
    listing_id, listing_version_number,
    product_version_id, supplier_id, rate_id, availability_id,
    supplier_name, product_name, channel_code, product_type_code, geography,
    audience_profile_json, rate_type_code, amount_minor, currency_code,
    availability_code, rate_effective_from, rate_effective_to,
    rate_source_locator, availability_source_locator,
    availability_observed_at_utc, availability_valid_until_utc,
    supplier_vat_status_code, vat_treatment_code,
    supplier_commercial_json, commercial_terms_json,
    deliverable_json, spatial_json, NULL,
    private_spatial_location, private_coverage_geometry,
    private_catchment_geometry, private_route_geometry,
    terms, current_setting('app.current_actor_id')::uuid, clock_timestamp()
FROM inventory_marketplace_seed_pending;

UPDATE commercial.marketplace_listings listing
SET current_version_id = pending.listing_version_id,
    status_code = 'PUBLISHED',
    archived_reason = NULL,
    version = listing.version + 1,
    updated_at_utc = clock_timestamp()
FROM inventory_marketplace_seed_pending pending
WHERE listing.supplier_tenant_id =
      current_setting('app.current_tenant_id')::uuid
  AND listing.id = pending.listing_id
  AND listing.status_code <> 'ARCHIVED';

UPDATE commercial.marketplace_listings listing
SET status_code = 'ARCHIVED',
    archived_reason = 'Current inventory is not eligible for marketplace publication.',
    version = listing.version + 1,
    updated_at_utc = clock_timestamp()
WHERE listing.supplier_tenant_id =
      current_setting('app.current_tenant_id')::uuid
  AND listing.created_by = current_setting('app.current_actor_id')::uuid
  AND listing.status_code = 'PUBLISHED'
  AND NOT EXISTS (
      SELECT 1
      FROM inventory_marketplace_seed_eligible eligible
      WHERE eligible.listing_id = listing.id);

SELECT
    count(DISTINCT listing.id) FILTER (WHERE listing.status_code = 'PUBLISHED')
        AS published_marketplace_listings,
    count(version.id) AS marketplace_listing_versions
FROM commercial.marketplace_listings listing
LEFT JOIN commercial.marketplace_listing_versions version
  ON version.supplier_tenant_id = listing.supplier_tenant_id
 AND version.listing_id = listing.id
WHERE listing.supplier_tenant_id =
      current_setting('app.current_tenant_id')::uuid
  AND listing.created_by = current_setting('app.current_actor_id')::uuid;

COMMIT;
