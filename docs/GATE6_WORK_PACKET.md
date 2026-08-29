# Gate 6 work packet — inventory truth

## Status

**AUTHORISED FOR LOCAL IMPLEMENTATION — standing direction, 2026-08-29**

Brian Rabuthu directed sequential local gates to continue without approval pauses and directed
the complete contents of each delivered gate to be committed automatically. Gate 5 is committed
at `5975f32`. This packet records the exact Gate 6 boundary before implementation.

Authority remains local and non-production. It does not permit a push, deployment, shared-database
migration, cloud mutation, live or paid AI, production data, supplier contact, booking, spend, or
external publication.

## Bounded outcome

A tenant-authorised inventory operator can upload a supplier file, execute a deterministic
protection and extraction pipeline, review evidence-linked canonical candidates, correct or reject
them with a reason, publish accepted candidates, and find the resulting versioned products through
cursor-paginated search and detail APIs and authenticated browser journeys.

Gate 6 ends at searchable inventory truth. It does not promise current supplier availability,
perform supplier self-service, enrich from a live provider, plan media, request quotes, communicate,
book, fund, or publish externally.

## Governing sources

- Normative Sections 7.4, 10, 15, 18.4, 19.3, 20.4, 21.4–21.5, 23 and 24;
- `docs/IMPLEMENTATION_PLAN.md`, Gate 6;
- accepted ADR-0002, ADR-0005 through ADR-0009;
- the governed master-data registry and permission matrix.

## Exact implementation packet

### Intake and protection

- Accept CSV, XLSX, PDF, DOCX, PNG and JPEG up to 100 MiB through an API-owned upload intent.
- Detect content from bytes; filenames are descriptive only and a type mismatch fails closed.
- Store bytes in S3-compatible object storage, first under quarantine and then under an immutable
  SHA-256-addressed protected key. No file binary is stored in PostgreSQL.
- Require a malware-scan result before extraction. Local acceptance uses deterministic fakes;
  the runtime adapter targets the local ClamAV service and fails closed when unavailable.
- Persist import state and step outcomes so retries are safe and never duplicate candidates.

### Extraction and canonical candidates

- Extract tabular CSV/XLSX and text from DOCX/PDF deterministically. Image files remain valid source
  assets but do not invent products without a human-supplied correction.
- Use declarative field aliases, never supplier-name conditionals.
- Preserve raw value, normalized value, transformation, source locator and source hash per field.
- Validate common identity, channel, geography and rate facts. OOH additionally validates site/media
  geography; Radio additionally validates station/market facts. All governed channels can remain as
  reviewable extension data.
- Missing product identity, channel, currency/rate identity or geography is a publish blocker.
  `UNKNOWN` availability is permitted as a warning and blocks later booking, not Gate 6 review.
  Unsupported audience claims are excluded rather than inferred.

### Human review and publication

- Assigned inventory operators may edit proposed values, approve or reject a candidate, with an
  append-only decision and governed rejection reason.
- Only exact current approved candidates may publish. Publication creates or advances immutable
  product versions plus rates, availability and source assets in one idempotent audited command.
- Import creators cannot be their own sole reviewer/publisher. Platform publication still requires
  a named human with the exact permission and artefact version.

### Search and experience

- Provide the normative create/execute/status/review/publish and product list/detail API surfaces.
- Product search supports opaque cursor pagination plus channel, supplier, geography and text filters;
  acceptance proves deterministic paging over more than 10,000 records.
- Provide authenticated import review, inventory search and product detail routes with loading,
  empty, forbidden, error and recovery states and source-evidence visibility.

## Acceptance evidence

Gate 6 delivery requires repeatable evidence for:

1. disposable PostgreSQL apply/reapply/rollback, forced RLS and cross-tenant denial;
2. file size, byte-signature, mismatch, malicious/unscanned and immutable-hash boundaries;
3. representative held-out CSV, XLSX, PDF, DOCX and image fixtures without supplier-specific code;
4. field lineage, blocking validation, edit/approve/reject and creator-separation rules;
5. idempotent publication and immutable version history;
6. filtered cursor paging over at least 10,001 products and dedicated detail;
7. authenticated desktop and compact import-to-search browser journeys;
8. affected Release builds, C#, Python, web, architecture and Compose checks;
9. retained `docs/evidence/gate-6/` report and machine-readable manifest.

## Explicit exclusions

- No live AI/OCR, web acquisition, audience enrichment, inventory availability assertion, supplier
  integration, quote, negotiation, plan, proposal, external publication, commitment or spend.
- No direct Python access to PostgreSQL or object storage credentials.
- No API startup migration and no migration of the shared local database.
- No claim of production, security, privacy, legal or independent-review approval.
