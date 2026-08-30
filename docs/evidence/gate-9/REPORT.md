# Gate 9 evidence report — OOH-only campaign mode and Proposal inbox

**Evidence date:** 2026-08-30

**Repository:** advertified-commercial-os2

**Branch:** master

**Owner direction:** commit and push all verified local changes, then continue sequential gates

**Live provider used:** No

**Production resources used:** No

**Incremental AI cost:** 0

## Delivered capability

OOH is now an immutable media selection on the same canonical campaign lifecycle used by every campaign: approved Brief → segmentation, targeting and positioning → editable media mix → verified inventory → approved media plan → proposal → client decision. `OOH_ONLY` permits only OOH and DOOH allocations. `FULL_CAMPAIGN` permits the governed active media set. Neither mode can be changed after selection; changed media scope starts a new CampaignBrief and carries no planning artefact into the new campaign.

The supplied-Brief entry can accept pasted or typed source material, structure the client, objective, audience, geography, timing, budget, VAT state and required media, and decide the campaign mode when evidence is clear. No client record must exist beforehand: a clearly named client is created canonically as part of Brief intake. A person is shown only fields the understanding service could not resolve.

A tenant-owned Proposal inbox can receive a signed provider notification, retrieve and retain the immutable email, and execute the same OOH-only canonical flow without a per-message click. A complete request produces STP, an OOH/DOOH allocation, benchmarked eligible inventory, an approved plan, proposal, deterministic branded PDF and one idempotent reply to the verified address. Incomplete, attached, multi-channel or commercially unready requests stop visibly and send nothing.

## Important invariants verified

- there is one canonical planning workflow; no `RapidOoh` aggregate, endpoint, migration, permission or source tree is permitted;
- the only OOH/full-campaign difference is the allowed media set;
- campaign mode is selected once and cannot be widened, converted or restarted in place;
- both campaign modes use full segmentation, targeting and positioning;
- clear Briefs need no routine correction or confirmation screen;
- ambiguous Briefs expose only their unresolved questions;
- the original supplied text or email remains immutable when a clarification is supplied;
- a clearly named client is created canonically without prior client registration;
- the automatic route is restricted to OOH/DOOH and requires current confirmed supply for every selected placement and running period;
- the standard planner remains channel-neutral and can retain indicative supply with transparent objections;
- every media allocation retains its own running periods and reconciled budget;
- OOH comparisons use compatible PostGIS local cohorts and retained deterministic statistics;
- proposal generation binds the exact approved plan, current rates, availability and dates;
- duplicate provider notifications and duplicate source content return the same inbound record and cannot send twice;
- retry after an unclear field reuses completed work and persisted understanding rather than duplicating paid work;
- started, review-required, failed and sent run transitions are recorded in audit and outbox records;
- provider payload, retrieval and delivery failures map to human-safe typed boundaries;
- local verification used deterministic adapters and contacted no external recipient or production resource.

## Verification

| Check | Result |
|---|---|
| Isolated Release C# API build | PASS — 0 warnings, 0 errors |
| Complete C# API suite | PASS — 41/41 |
| Email-to-proposal PostgreSQL acceptance | PASS — auto-created client, complete STP, immutable OOH mode, benchmarked confirmed inventory, approved plan, proposal, PDF and exactly-once reply |
| Exception paths | PASS — duplicate, non-OOH and incomplete Brief paths send nothing; missing audience is corrected through the single outstanding question and then completes |
| Audit and outbox | PASS — automation started and sent transitions retained against the exact run |
| Architecture guardrails | PASS — 23/23, including explicit rejection of any parallel Rapid OOH workflow |
| Agent runtime tests | PASS — 18/18 with deterministic provider mode and no paid model call |
| Web lint | PASS — 0 warnings, 0 errors |
| Web unit tests | PASS — 4/4 |
| Web production build | PASS — Proposal inbox is separately lazy-loaded |
| Authenticated Playwright | PASS — 18/18 desktop + compact, including the Proposal inbox |
| Retained OpenAPI v1 | PASS — regenerated contract matches the running API and includes mailbox, message, clarification and retry boundaries |

## User experience result

The Proposal inbox follows the approved Advertified navy, white, neutral and electric-blue visual system and the Omnicom Omni interaction direction without copying its branding. The layout is spacious: mailbox state, four concise operational totals, message list and one selected request. The request view progressively discloses the automatic stages, original email, approved artefact links and only the corrections required. Internal payloads, provider diagnostics, scores and technical exception text are not exposed.

The ordinary Brief page follows the same principle. The user pastes or types the source, the system structures it and selects OOH-only or full campaign when evidence is clear. A clarification form appears only when a material detail is genuinely ambiguous.

## Remaining boundaries

Supplier self-service, RFQ and supplier-response operations remain Gate 10. Funding, booking, delivery, proof, measurement and learning remain Gate 11. Independent security/privacy/operations review, shared-environment migration and production provider configuration remain later gates. No live Resend request, supplier contact, booking, spend, payment, invoice or production deployment was performed.

Local implementation and repeatable checks are complete. The implementing AI did not approve Gate 9, security, privacy, legal compliance or production readiness; those decisions remain with the owner and independent reviewers.
