# Advertified risk register

**Status:** ACTIVE DRAFT  
**Evidence date:** 2026-08-29  
**Named owners:** UNASSIGNED — no risk may be accepted or closed

Likelihood and impact are preliminary (L/M/H) until named owners assess them. “Mitigation” describes required work, not completed control.

| ID | Risk | L | I | Detection | Required mitigation | Contingency | Owner function |
|---|---|:---:|:---:|---|---|---|---|
| R01 | Cross-tenant data leakage | M | H | Negative auth/query/object/job tests; access alerts | Tenant on every protected resource and command; deny by default; DB defence in depth | Disable affected route; contain tenant scope; incident process | Security + API |
| R02 | Broken role/assignment enforcement | M | H | Permission matrix tests and audit anomaly | Server resolves identity, membership, assignment, state | Revoke session/role; fail closed | Security + Product |
| R03 | Creator self-approves consequence | M | H | Approval audit invariant | Separate edit/submit/approve permissions and named approver | Invalidate approval and dependent artefacts | Product + API |
| R04 | Prompt injection changes tool policy | H | H | Adversarial fixtures; tool-denial telemetry | Treat content as data; allowlisted tools; independent API authorisation | Stop run; quarantine source; human review | AI + Security |
| R05 | Evidence is misinterpreted | H | H | Citation/field review and evaluation corpus | Approved evidence bindings, unknowns, human gate, critic | Reject artefact; correct evidence; rerun only changed inputs | Product + AI |
| R06 | Unsupported demographic inference | M | H | Schema/policy validation and review | Prohibit sensitive inference without approved source and purpose | Remove claim; privacy escalation | Privacy + AI |
| R07 | Inventory/rate/availability invented | M | H | Provenance and freshness assertions | Verified supplier/source records only; human confirmation | Mark unavailable; block plan/booking | Inventory + Commercial |
| R08 | Stale rate or availability reused | H | H | Effective-date/freshness checks | Versioned records; stale state visible; re-confirm thresholds | Reprice and reapprove exact version | Inventory + Commercial |
| R09 | Supplier silence treated as confirmation | H | H | RFQ timeout state | Explicit pending/expired/unavailable outcomes | Hold or select another verified option after human action | Supplier Ops |
| R10 | Pricing/proposal drift after approval | M | H | Hash/version reconciliation | Bind immutable plan, rates, assumptions, wording and totals | Invalidate proposal; regenerate and reapprove | Commercial + API |
| R11 | Money/VAT/fee calculation error | M | H | Deterministic examples/property tests | Typed minor-unit money and governed policies; no AI arithmetic authority | Block proposal/payment; corrected calculation and reapproval | Finance + API |
| R12 | Duplicate paid provider call | M | M | Usage ledger and idempotency metrics | Checkpoints, input hash, retry policy, cost cap | Disable provider; reconcile cost; reuse validated result | AI + Operations |
| R13 | Provider cost spike | M | H | Per-run/tenant/global cost alerts | Default zero; approved caps; deny before call | Circuit open; deterministic test mode; no live fallback | Finance + AI |
| R14 | Provider outage or invalid output | H | M | Timeout/schema/circuit metrics | Typed validation, bounded retry, review-required state | Queue/stop safely; no alternative live provider without ADR | AI + Operations |
| R15 | Worker restart duplicates consequence | M | H | Idempotency and chaos/restart tests | Durable checkpoint, inbox/outbox, unique consequence key | Reconcile and compensate through authorised command | Platform + API |
| R16 | Malicious upload | M | H | Type/malware/quarantine telemetry | Immutable quarantine, safe rendering, least privilege | Isolate/delete per policy; incident review | Security + Inventory |
| R17 | Extraction corrupts catalogue | H | H | Precision/recall corpus and reconciliation | Preserve source; deterministic validation; human review | Reject batch; correct parser; republish version | Inventory + Data |
| R18 | Bad geography/route resolution | M | H | Map fixture and boundary validation | Store source/CRS/precision; human-visible uncertainty | Exclude candidate; manual verified correction as governed data | Data + Planning |
| R19 | Migration incompatibility/data loss | M | H | Empty/upgrade/restore tests | Expand-migrate-contract; backups; forward-safe migration | Stop rollout; restore or approved compensating migration | Database + Operations |
| R20 | Backup exists but restore fails | M | H | Scheduled restore drill | Encrypted tested backups and RPO/RTO evidence | Incident recovery plan; no launch until restored | Operations |
| R21 | Partial deployment breaks contract | M | H | Version/compatibility smoke and canary | Backward-compatible API/events/migrations | Halt; roll forward/back per tested plan | Platform |
| R22 | Identity provider/session delay or flaw | M | H | Auth threat tests and provider monitoring | Approved OIDC/session/CSRF/revocation design | Disable affected login/action; no insecure local bypass | Security |
| R23 | Email/maps/payment integration delay | H | M | Contract sandbox and readiness checks | Provider-neutral boundary; explicit disabled state | Keep dependent capability blocked; no invented success | Integrations |
| R24 | Privacy/legal approval delayed | H | H | Decision register age | Assign owners early; data map and contracts before processing | Reduce scope; block processing/launch | Privacy + Legal |
| R25 | Missing named gate owner | H | H | Entry-gate check | Assign accountable human before implementation | Gate stays blocked | Executive/Product |
| R26 | Accessibility failure | M | M | Automated and manual keyboard/screen-reader review | Semantic components, focus, contrast, reduced motion | Block affected screen release | UX + QA |
| R27 | Performance fails at catalogue scale | M | H | Load plans, query metrics, browser profiling | Pagination, indexes, bounded queries, virtualisation | Throttle/disable affected path; capacity correction | Platform + Data |
| R28 | Observability misses commercial failure | M | H | Synthetic journeys and alert tests | Correlated logs/metrics/traces plus business outcome signals | Manual reconciliation; add detector before gate close | Operations + Product |

## Review rule

Each gate packet selects affected risks, assigns actual names, records pre/post rating, links tests and telemetry, and identifies a tested contingency. A role label or AI-generated statement is not acceptance.
