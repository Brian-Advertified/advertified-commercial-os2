# Gate 6 evidence report

**Evidence date:** 2026-08-29

**Repository/branch:** advertified-commercial-os2 / master

**Base commit:** `5975f32d145da44f5012a23c0a6971edfe60d062`

**Working tree at evidence capture:** uncommitted review before the owner-directed automatic commit

**Owner direction:** continue sequential local gates without repetitive approval pauses, commit
each complete gate automatically, and accept inventory files up to 100 MiB — Brian Rabuthu,
2026-08-29

## Delivered outcome

Gate 6 now turns a tenant-authorised supplier file into protected, evidence-linked inventory.
The API classifies CSV, XLSX, PDF, DOCX, PNG, and JPEG from bytes, rejects filename/content
mismatches and files above 100 MiB, quarantines and scans source bytes, and promotes clean
content to an immutable SHA-256-addressed MinIO key. PostgreSQL stores lineage and state, not
binary content.

`InventoryProtection__MaximumSourceBytes` is the one application upload-limit setting. The
authenticated inventory response exposes its active value so the browser uses the same policy.
PostgreSQL and ClamAV retain fixed 100 MiB guardrails and the API rejects a configured value above
those ceilings.

The runtime extraction path uses Docling Serve 1.30.0 with Docling 2.118.0 behind an
Advertified-owned versioned adapter. Each source creates an immutable checkpoint containing
the source hash, adapter and schema versions, structured Docling JSON, and output hash before
candidate construction. Deterministic local extraction is restricted to test fixtures. Each
field retains its raw value, normalised value, transformation, source locator, and source hash.
Missing or invalid identity, channel, geography, and rate facts block publication; image-only
sources remain blocked for human correction instead of inventing products. Unsupported audience
claims are excluded.

A distinct assigned inventory operator reviews, corrects, approves, or rejects candidates.
Publication creates immutable product, rate, availability, and source-asset versions. Search and
detail APIs use tenant enforcement and deterministic cursor paging. Authenticated browser routes
cover upload, extraction, evidence review, publication preview, search, and product lineage.

## Verification

| Check | Outcome |
|---|---|
| Commercial API and migration runner Release builds | PASS — .NET 10/C# 14, 0 warnings and 0 errors |
| Complete C# suite | PASS — 34 passed |
| Gate 6 disposable PostgreSQL acceptance | PASS — 1 end-to-end lifecycle with protected state and cross-tenant denial |
| Held-out document corpus | PASS — 6/6 byte classifications; critical fields extracted for 4/4 structured sources; 2/2 image-only sources blocked |
| 100 MiB source boundary | PASS — 100 MiB + 1 byte rejected by the HTTP/API validation path |
| Search scale | PASS — deterministic distinct cursor pages after inserting 10,001 products |
| Agent runtime | PASS — 16 passed; Ruff passed |
| Architecture guardrails | PASS — 21 passed |
| Web lint/tests/build | PASS — lint, type-check, 4 tests, and production build passed |
| Browser acceptance | PASS — 12 cases across desktop and compact viewports |
| Web runtime dependency audit | PASS — 0 vulnerabilities |
| Retained OpenAPI v1 | PASS — Gate 6 routes present and contract checks pass |
| Compose validation and health | PASS — PostgreSQL, Redis, MinIO, ClamAV, MailHog, and pinned Docling healthy |
| ClamAV effective limits | PASS — 100 MiB stream/file and 400 MiB aggregate scan limits |

The versioned Docling adapter contract, real authenticated CSV conversion sandbox and immutable
extraction checkpoint also pass. The
database-backed acceptance proves malware isolation, exact hash verification,
creator/reviewer separation, immutable review decisions, versioned publication, cross-tenant
denial, and human-verified product detail. No live OCR or AI is used.

## Corrected checks

1. The first architecture run found governed inventory codes embedded in application/UI files
   and missing MinIO/PdfPig dependency-policy entries. Codes were centralised and the explicit
   dependency contract updated; the final architecture run passed 21/21.
2. The first production web build exposed overly narrow inferred literal types after vocabulary
   centralisation. The role and reason sets were typed as governed strings; the final build and
   all 12 browser cases passed.
3. The original packet used a 25 MiB boundary. Brian Rabuthu changed it to 100 MiB. API multipart
   handling, validation, migration constraint, work packet, UI, and acceptance evidence now agree.
4. The pinned ClamAV daemon was checked after the limit change. Its effective stream and file
   ceilings are both 104,857,600 bytes, so the runtime scanner matches the API contract.
5. Consolidating the limit initially exposed top-level complexity, paginated-state typing, and
   generic test-host protection configuration failures. Validation moved into cohesive option
   predicates, pagination now carries the server policy, and one deterministic test-host helper
   configures protection. The final builds and complete suites pass.
6. A post-delivery audit found that the first Gate 6 implementation used only local parsers even
   though the normative technology lock requires Docling. The correction adds the pinned Docling
   service and provider-neutral adapter, retains raw structured output and parser identity, and
   confines the prior parser to deterministic tests.

## Safety and remaining non-local work

- Live or paid provider used: No.
- Incremental AI cost: 0 minor units.
- Production resource or data used: No.
- Main/shared local database migration applied: No.
- Supplier contact, booking, spend, or external publication: No.
- Local infrastructure mutation: the six development Compose services were built/started and
  health-checked; no data volume was deleted.
- Commit: included in the owner-directed automatic Gate 6 commit; no push, merge, release, or deployment.
- Pending: remote CI and independent Engineering, Security/Privacy, and Operations review.

The local Gate 6 implementation and repeatable evidence are complete. The implementing AI did
not approve security, privacy, legal compliance, or production readiness.
