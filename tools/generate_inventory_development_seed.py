"""Generate an idempotent local PostgreSQL fixture from the reviewed inventory seed."""
from __future__ import annotations
import argparse
import hashlib
import json
import uuid
from pathlib import Path

TENANT_ID = "10000000-0000-0000-0000-000000000002"
CREATOR_ID = "10000000-0000-0000-0000-000000000001"
REVIEWER_ID = "10000000-0000-0000-0000-000000000021"
NAMESPACE = uuid.UUID("2dd2b302-d11c-4c3d-9ef0-40559dd1a73d")
MASTER_DATA_PATH = Path(__file__).resolve().parents[1] / "shared/contracts/master-data.json"

def stable_id(kind: str, key: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"{kind}:{key}"))

def canonical_hash(value: object) -> str:
    encoded = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()

def governed_codes() -> dict[str, str]:
    collections = json.loads(MASTER_DATA_PATH.read_text(encoding="utf-8"))["collections"]
    def find(collection: str, label: str) -> str:
        matches = [item["code"] for item in collections[collection]
                   if item["displayLabel"] == label and item["isActive"]]
        if len(matches) != 1:
            raise ValueError(f"expected one active {collection} item labelled {label}")
        return matches[0]

    return {
        "reviewer_role": find("roles", "Inventory Operations"),
        "active": find("lifecycleStatuses", "Active"),
        "inactive": find("lifecycleStatuses", "Inactive"),
        "archived": find("lifecycleStatuses", "Archived"),
        "review_required": find("lifecycleStatuses", "Review Required"),
        "completed": find("lifecycleStatuses", "Completed"),
        "unclaimed": find("supplierClaimStatuses", "Managed by Advertified"),
        "pdf": find("documentClasses", "PDF Document"),
        "presentation": find("documentClasses", "PowerPoint Presentation"),
        "workbook": find("documentClasses", "Excel Workbook"),
        "clean": find("malwareScanStatuses", "Clean"),
        "supplier_resolved": find("inventorySupplierResolutionStatuses", "Supplier Identified"),
        "supplier_ambiguous": find("inventorySupplierResolutionStatuses", "Supplier Requires Review"),
    }

def locator(record: dict) -> str:
    source = record["provenance"]
    unit = (f"page={source['page']}" if "page" in source else
            f"slide={source['slide']}" if "slide" in source else
            f"worksheet={source['worksheet']}")
    return f"{source['file']}#{unit};heading={source.get('heading', '')}"

def selectable_records(records: list[dict]) -> list[dict]:
    result: list[dict] = []
    for record in records:
        rates = record.get("rates")
        if not rates or len(rates) == 1 or record.get("ratePublicationMode") == "SHARED_PRODUCT":
            result.append(record)
            continue
        for index, rate in enumerate(rates, 1):
            qualifiers = [rate.get("days"), rate.get("daypart"), rate.get("geography"),
                          f"{rate['durationSeconds']} seconds" if rate.get("durationSeconds") else None]
            qualifier = ", ".join(value for value in qualifiers if value)
            item = {**record, "key": f"{record['key']}:rate:{index:03d}",
                    "name": f"{record['name']} — {qualifier}", "rates": [rate]}
            deliverable = {**(record.get("deliverable") or {})}
            if rate.get("daypart"):
                deliverable["daypart"] = rate["daypart"]
            if rate.get("durationSeconds"):
                deliverable["spotLengthSeconds"] = rate["durationSeconds"]
            item["deliverable"] = deliverable
            result.append(item)
    return result

def enrich(seed: dict) -> dict:
    sources = {item["file"]: item for item in seed["sources"]}
    documents = {item["file"]: item for item in seed.get("documents", [])}
    payload = {"seedVersion": seed["seedVersion"], "seedChecksum": canonical_hash(seed),
               "suppliers": [], "documents": [], "records": []}
    for supplier in seed["suppliers"]:
        evidence = next((record for record in seed["records"]
            if record["supplierKey"] == supplier["key"] and
            any(rate.get("vat") in {"EXCLUDES_VAT", "INCLUDES_VAT"}
                for rate in (record.get("rates") or
                    ([record["rate"]] if record.get("rate") else [])))), None)
        commercial = {} if evidence is None else {
            "commercialVersionId": stable_id(
                "supplier-commercial", f"{supplier['key']}:{seed['seedVersion']}"),
            "commercialImportId": stable_id("import", evidence["documentKey"]),
            "vatStatus": "REGISTERED",
        }
        payload["suppliers"].append({**supplier, **commercial,
            "id": stable_id("supplier", supplier["key"])})
    supplier_ids = {item["key"]: item["id"] for item in payload["suppliers"]}
    records_by_file: dict[str, list[dict]] = {}
    expanded_records = selectable_records(seed["records"])
    for record in expanded_records:
        records_by_file.setdefault(record["provenance"]["file"], []).append(record)
    for file_name, document in documents.items():
        source = sources[file_name]
        key = document["key"]
        rows = records_by_file.get(file_name, [])
        item = {**document, "sha256": source["sha256"], "sourceSize": source.get("sizeBytes", 0),
                "importId": stable_id("import", key), "extractionId": stable_id("extraction", key),
                "projectionId": stable_id("projection", key),
                "supplierId": supplier_ids[document["supplierKey"]],
                "hasReviewItems": any(not row.get("publicationEligible") for row in rows)}
        item["pricingSchedules"] = [value for value in seed.get("pricingSchedules", [])
                                    if value.get("documentKey") == key]
        item["planningEvidence"] = [value for value in seed.get("planningEvidence", [])
                                    if value.get("documentKey") == key]
        payload["documents"].append(item)
    document_ids = {item["file"]: item for item in payload["documents"]}
    for index, record in enumerate(expanded_records, 1):
        document = document_ids[record["provenance"]["file"]]
        rates = record.get("rates") or ([record["rate"]] if record.get("rate") else [])
        version_key = (
            f"{record['key']}:{seed['seedVersion']}:{canonical_hash(record)}"
        )
        item = {**record, "rowNumber": index, "candidateId": stable_id("candidate", record["key"]),
                "productId": stable_id("product", record["key"]),
                "versionId": stable_id("version", version_key),
                "availabilityId": stable_id("availability", version_key),
                "supplierId": supplier_ids[record["supplierKey"]],
                "importId": document["importId"], "projectionId": document["projectionId"],
                "sourceLocator": locator(record), "rates": []}
        for rate_index, rate in enumerate(rates, 1):
            item["rates"].append({**rate,
                "id": stable_id("rate", f"{version_key}:{rate_index}")})
        payload["records"].append(item)
    return payload

def sql_literal(payload: dict) -> str:
    value = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    if "$inventory$" in value:
        raise ValueError("seed contains the reserved SQL delimiter")
    return f"$inventory${value}$inventory$::jsonb"

def generate(payload: dict, codes: dict[str, str]) -> str:
    return f"""-- Generated by tools/generate_inventory_development_seed.py. Do not edit.
\\set ON_ERROR_STOP on
BEGIN;
SET LOCAL advertified.tenant_id = '{TENANT_ID}';
SET LOCAL advertified.user_id = '{CREATOR_ID}';
CREATE TEMP TABLE inventory_bootstrap_payload (value jsonb) ON COMMIT DROP;
INSERT INTO inventory_bootstrap_payload VALUES ({sql_literal(payload)});
CREATE TEMP TABLE inventory_bootstrap_candidate_rows ON COMMIT DROP AS
SELECT (r->>'candidateId')::uuid AS candidate_id, (r->>'importId')::uuid AS import_id,
       (r->>'rowNumber')::integer AS row_number, (r->>'productId')::uuid AS product_id,
       (r->>'publicationEligible')::boolean AS publication_eligible
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r;
CREATE UNIQUE INDEX ux_inventory_bootstrap_candidate_id ON inventory_bootstrap_candidate_rows(candidate_id);
CREATE UNIQUE INDEX ux_inventory_bootstrap_candidate_import_row ON inventory_bootstrap_candidate_rows(import_id, row_number);
CREATE INDEX ix_inventory_bootstrap_published_product ON inventory_bootstrap_candidate_rows(product_id)
    WHERE publication_eligible;

INSERT INTO commercial.memberships (id, tenant_id, user_id, role_code, status_code,
    invited_by, invited_at_utc, accepted_at_utc, version, created_at_utc, updated_at_utc)
VALUES ('{stable_id('membership', 'local-inventory-reviewer')}', '{TENANT_ID}', '{REVIEWER_ID}',
    '{codes['reviewer_role']}', '{codes['active']}', '{CREATOR_ID}', clock_timestamp(), clock_timestamp(), 1,
    clock_timestamp(), clock_timestamp()) ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_suppliers (id, tenant_id, name, external_reference,
    identity_key, claim_status_code, version, created_at_utc, updated_at_utc)
SELECT (s->>'id')::uuid, '{TENANT_ID}', s->>'name', s->>'key', lower(s->>'key'),
    '{codes['unclaimed']}', 1, clock_timestamp(), clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'suppliers') s
ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_suppliers supplier
SET name=s->>'name', updated_at_utc=clock_timestamp(), version=supplier.version + 1
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'suppliers') s
WHERE supplier.id=(s->>'id')::uuid
  AND supplier.claim_status_code='{codes['unclaimed']}'
  AND supplier.name IS DISTINCT FROM s->>'name';

INSERT INTO commercial.inventory_imports (id, tenant_id, supplier_id, source_file_name,
    declared_media_type, document_class_collection_code, document_class_code, status_code,
    scan_status_code, quarantine_object_key, protected_object_key, source_hash, source_size,
    created_by, version, created_at_utc, updated_at_utc, supplier_name_hint,
    supplier_resolution_status_code, supplier_identity_evidence_json)
SELECT (d->>'importId')::uuid, '{TENANT_ID}', (d->>'supplierId')::uuid, d->>'file',
    CASE WHEN lower(d->>'file') LIKE '%.pdf' THEN 'application/pdf'
         WHEN lower(d->>'file') LIKE '%.pptx' THEN 'application/vnd.openxmlformats-officedocument.presentationml.presentation'
         ELSE 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' END,
    'documentClasses', CASE WHEN lower(d->>'file') LIKE '%.pdf' THEN '{codes['pdf']}'
         WHEN lower(d->>'file') LIKE '%.pptx' THEN '{codes['presentation']}' ELSE '{codes['workbook']}' END,
    CASE WHEN (d->>'hasReviewItems')::boolean THEN '{codes['review_required']}' ELSE '{codes['completed']}' END,
    '{codes['clean']}', 'manual-bootstrap/' || (d->>'sha256'),
    'manual-bootstrap/' || (d->>'sha256'),
    d->>'sha256', GREATEST((d->>'sourceSize')::bigint, 0), '{CREATOR_ID}', 1,
    clock_timestamp(), clock_timestamp(), s->>'name',
    CASE WHEN s->>'status' = 'SOURCE_IDENTIFIED' THEN '{codes['supplier_resolved']}' ELSE '{codes['supplier_ambiguous']}' END,
    jsonb_build_object('method','manual_physical_review','sourceVersion',d->>'sourceVersion')
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'documents') d
JOIN LATERAL (SELECT value s FROM jsonb_array_elements(p.value->'suppliers')
    WHERE value->>'id'=d->>'supplierId') supplier ON true
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_supplier_versions (id, tenant_id, supplier_id,
    version_number, vat_status_code, source_import_id, published_by, published_at_utc)
SELECT (s->>'commercialVersionId')::uuid, '{TENANT_ID}', (s->>'id')::uuid,
    COALESCE((SELECT max(existing.version_number) + 1
      FROM commercial.inventory_supplier_versions existing
      WHERE existing.supplier_id=(s->>'id')::uuid), 1),
    s->>'vatStatus', (s->>'commercialImportId')::uuid, '{REVIEWER_ID}', clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'suppliers') s
WHERE s ? 'commercialVersionId' ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_suppliers supplier
SET current_commercial_version_id=(s->>'commercialVersionId')::uuid,
    updated_at_utc=clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'suppliers') s
WHERE supplier.id=(s->>'id')::uuid AND s ? 'commercialVersionId'
  AND supplier.current_commercial_version_id IS DISTINCT FROM (s->>'commercialVersionId')::uuid;

INSERT INTO commercial.inventory_extractions (id, tenant_id, import_id, source_hash,
    adapter_code, adapter_version, schema_version, provider_json, provider_output_hash,
    completed_at_utc, canonical_json, canonical_output_hash, source_file_version)
SELECT (d->>'extractionId')::uuid, '{TENANT_ID}', (d->>'importId')::uuid, d->>'sha256',
    'MANUAL_PHYSICAL_REVIEW', '1.0.0', 'advertified.production-inventory-bootstrap.v1',
    '{{"provider":"human-reviewed-source"}}'::jsonb, d->>'sha256', clock_timestamp(), d,
    encode(digest(d::text, 'sha256'),'hex'), 1
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'documents') d
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_extraction_projections (id, tenant_id, import_id,
    input_artifact_id, projector_code, projector_version, schema_version, canonical_json,
    canonical_output_hash, candidate_count, created_by, created_at_utc)
SELECT (d->>'projectionId')::uuid, '{TENANT_ID}', (d->>'importId')::uuid,
    (d->>'extractionId')::uuid, 'MANUAL_PHYSICAL_REVIEW', '1.0.0',
    'advertified.production-inventory-bootstrap.v1', d,
    encode(digest(d::text, 'sha256'),'hex'),
    (SELECT count(*) FROM jsonb_array_elements(p.value->'records') r
WHERE r->>'importId'=d->>'importId'), '{CREATOR_ID}', clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'documents') d
ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_candidates candidate
SET row_number = candidate.row_number + 10000000
FROM inventory_bootstrap_candidate_rows desired
WHERE candidate.tenant_id = '{TENANT_ID}'
  AND desired.import_id=candidate.import_id
  AND desired.row_number=candidate.row_number
  AND desired.candidate_id<>candidate.id
  AND candidate.row_number < 10000000;

INSERT INTO commercial.inventory_candidates (id, tenant_id, import_id, row_number,
    status_code, proposed_values_json, canonical_values_json, validation_json,
    source_locator, reviewed_by, version, created_at_utc, updated_at_utc, projection_id)
SELECT (r->>'candidateId')::uuid, '{TENANT_ID}', (r->>'importId')::uuid,
    (r->>'rowNumber')::integer,
    CASE WHEN (r->>'publicationEligible')::boolean THEN 'APPROVED' ELSE '{codes['review_required']}' END,
    r, r, CASE WHEN (r->>'publicationEligible')::boolean THEN '[]'::jsonb
      ELSE jsonb_build_array(jsonb_build_object('code','MANUAL_REVIEW_REQUIRED','message',r->>'issue')) END,
    r->>'sourceLocator', CASE WHEN (r->>'publicationEligible')::boolean THEN '{REVIEWER_ID}'::uuid END,
    1, clock_timestamp(), clock_timestamp(), (r->>'projectionId')::uuid
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
ON CONFLICT (id) DO UPDATE SET
    row_number=EXCLUDED.row_number,
    status_code=EXCLUDED.status_code,
    proposed_values_json=EXCLUDED.proposed_values_json,
    canonical_values_json=EXCLUDED.canonical_values_json,
    validation_json=EXCLUDED.validation_json,
    source_locator=EXCLUDED.source_locator,
    reviewed_by=EXCLUDED.reviewed_by,
    projection_id=EXCLUDED.projection_id,
    soft_deleted_at_utc=NULL,
    superseded_at_utc=NULL,
    version=commercial.inventory_candidates.version + 1,
    updated_at_utc=clock_timestamp()
WHERE commercial.inventory_candidates.row_number IS DISTINCT FROM EXCLUDED.row_number
   OR commercial.inventory_candidates.status_code IS DISTINCT FROM EXCLUDED.status_code
   OR commercial.inventory_candidates.proposed_values_json IS DISTINCT FROM EXCLUDED.proposed_values_json
   OR commercial.inventory_candidates.canonical_values_json IS DISTINCT FROM EXCLUDED.canonical_values_json
   OR commercial.inventory_candidates.validation_json IS DISTINCT FROM EXCLUDED.validation_json
   OR commercial.inventory_candidates.source_locator IS DISTINCT FROM EXCLUDED.source_locator
   OR commercial.inventory_candidates.reviewed_by IS DISTINCT FROM EXCLUDED.reviewed_by
   OR commercial.inventory_candidates.projection_id IS DISTINCT FROM EXCLUDED.projection_id
   OR commercial.inventory_candidates.soft_deleted_at_utc IS NOT NULL
   OR commercial.inventory_candidates.superseded_at_utc IS NOT NULL;

UPDATE commercial.inventory_candidates candidate
SET soft_deleted_at_utc=clock_timestamp(), superseded_at_utc=clock_timestamp(),
    version=candidate.version + 1, updated_at_utc=clock_timestamp()
FROM inventory_bootstrap_payload p
WHERE candidate.tenant_id = '{TENANT_ID}'
  AND EXISTS (SELECT 1 FROM jsonb_array_elements(p.value->'documents') d
      WHERE (d->>'importId')::uuid = candidate.import_id)
  AND NOT EXISTS (SELECT 1 FROM inventory_bootstrap_candidate_rows desired
      WHERE desired.candidate_id = candidate.id)
  AND candidate.soft_deleted_at_utc IS NULL;

INSERT INTO commercial.inventory_products (id, tenant_id, supplier_id, supplier_product_code,
    status_code, version, created_at_utc, updated_at_utc)
SELECT (r->>'productId')::uuid, '{TENANT_ID}', (r->>'supplierId')::uuid,
    COALESCE(NULLIF(r->>'code',''), r->>'key'), '{codes['active']}', 1, clock_timestamp(), clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE (r->>'publicationEligible')::boolean ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_products product
SET status_code='{codes['active']}', expired_at_utc=NULL, version=product.version + 1,
    updated_at_utc=clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE product.id=(r->>'productId')::uuid AND (r->>'publicationEligible')::boolean
  AND (product.status_code <> '{codes['active']}' OR product.expired_at_utc IS NOT NULL);

UPDATE commercial.inventory_products product
SET status_code='{codes['inactive']}', expired_at_utc=COALESCE(product.expired_at_utc, clock_timestamp()),
    version=product.version + 1, updated_at_utc=clock_timestamp()
FROM inventory_bootstrap_payload p
CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE product.id=(r->>'productId')::uuid
  AND NOT (r->>'publicationEligible')::boolean
  AND product.status_code <> '{codes['inactive']}';

UPDATE commercial.inventory_products product
SET status_code='{codes['inactive']}', expired_at_utc=COALESCE(product.expired_at_utc, clock_timestamp()),
    version=product.version + 1, updated_at_utc=clock_timestamp()
FROM commercial.inventory_product_versions current_version, inventory_bootstrap_payload p
WHERE current_version.id=product.current_version_id AND current_version.tenant_id=product.tenant_id
  AND EXISTS (SELECT 1 FROM jsonb_array_elements(p.value->'documents') document
      WHERE (document->>'importId')::uuid=current_version.source_import_id)
  AND NOT EXISTS (SELECT 1 FROM inventory_bootstrap_candidate_rows desired
      WHERE desired.publication_eligible AND desired.product_id=product.id)
  AND product.status_code <> '{codes['inactive']}';

INSERT INTO commercial.inventory_product_versions (id, tenant_id, product_id, version_number,
    name, channel_code, product_type_code, geography, address, latitude, longitude,
    description, extension_json, audience_profile_json, deliverable_json, spatial_json,
    verification_code, source_import_id, source_candidate_id, published_by, published_at_utc)
SELECT (r->>'versionId')::uuid, '{TENANT_ID}', (r->>'productId')::uuid,
    COALESCE((SELECT max(existing.version_number) + 1
      FROM commercial.inventory_product_versions existing
      WHERE existing.product_id=(r->>'productId')::uuid), 1), r->>'name',
    r->>'channel', r->>'productType', COALESCE(r#>>'{{spatial,city}}',r#>>'{{spatial,province}}',
      r#>>'{{spatial,country}}',r->>'placement','Not location-specific'),
    r#>>'{{spatial,address}}', NULLIF(r#>>'{{spatial,latitude}}','')::numeric,
    NULLIF(r#>>'{{spatial,longitude}}','')::numeric, r->>'description',
    jsonb_build_object('bootstrapKey',r->>'key','documentKey',r->>'documentKey',
      'format',r->>'format','placement',r->>'placement',
      'pricingStatus',COALESCE(r->>'pricingStatus','PUBLISHED_RATE'),
      'package',COALESCE((r->'package')::text,''),
      'planning',COALESCE((r->'planning')::text,''),
      'provenance',COALESCE((r->'provenance')::text,'')), NULL,
    r->'deliverable', r->'spatial', 'HUMAN_VERIFIED', (r->>'importId')::uuid,
    (r->>'candidateId')::uuid, '{REVIEWER_ID}', clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE (r->>'publicationEligible')::boolean ON CONFLICT (id) DO NOTHING;

UPDATE commercial.inventory_products product SET current_version_id=(r->>'versionId')::uuid,
    updated_at_utc=clock_timestamp()
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE product.id=(r->>'productId')::uuid AND (r->>'publicationEligible')::boolean
  AND product.current_version_id IS DISTINCT FROM (r->>'versionId')::uuid;

INSERT INTO commercial.inventory_rates (id, tenant_id, product_version_id, rate_type_code,
    currency_code, amount_minor, effective_from, effective_to, vat_treatment_code,
    commercial_terms_json, source_locator, variant_json)
SELECT (rate->>'id')::uuid, '{TENANT_ID}', (r->>'versionId')::uuid,
    rate->>'rateType', rate->>'currency', (rate->>'amountMinor')::bigint,
    NULLIF(rate->>'validFrom','')::date, NULLIF(rate->>'validTo','')::date,
    CASE rate->>'vat' WHEN 'EXCLUDES_VAT' THEN 'EXCLUSIVE' WHEN 'INCLUDES_VAT' THEN 'INCLUSIVE'
      ELSE NULLIF(rate->>'vat','') END,
    jsonb_build_object('basis',rate->'basis','conditions',rate->'conditions',
      'additionalCosts',rate->'additionalCosts','billingDays',rate->'billingDays',
      'inclusions',rate->'inclusions'), r->>'sourceLocator',
    rate - 'id' - 'amountMinor' - 'currency' - 'rateType' - 'vat'
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
CROSS JOIN LATERAL jsonb_array_elements(r->'rates') rate
WHERE (r->>'publicationEligible')::boolean
  AND rate->>'amountMinor' IS NOT NULL AND rate->>'currency' IS NOT NULL
  AND rate->>'rateType' IS NOT NULL
ON CONFLICT (id) DO NOTHING;

INSERT INTO commercial.inventory_availability (id, tenant_id, product_version_id,
    availability_code, observed_at_utc, source_locator)
SELECT (r->>'availabilityId')::uuid, '{TENANT_ID}', (r->>'versionId')::uuid,
    r->>'availability', clock_timestamp(), r->>'sourceLocator'
FROM inventory_bootstrap_payload p CROSS JOIN LATERAL jsonb_array_elements(p.value->'records') r
WHERE (r->>'publicationEligible')::boolean ON CONFLICT (id) DO NOTHING;
COMMIT;
"""

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("seed", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    seed = json.loads(args.seed.read_text(encoding="utf-8"))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(generate(enrich(seed), governed_codes()), encoding="utf-8", newline="\n")
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
