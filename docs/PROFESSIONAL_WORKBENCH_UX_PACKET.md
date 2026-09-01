# Professional workbench UX adoption packet

**Owner direction:** Rebuild the authenticated product with the supplied Google AI prototype's actual screen composition and professional operating density. Treat its visual structure as the approved reference, while rejecting its demo data and business behaviour wherever they conflict with Advertified's canonical rules.

## Product decision

Advertified will adopt the prototype's professional workbench qualities:

- operational queues rather than decorative dashboards;
- campaign context that stays visible while work advances;
- evidence and source material beside the decision they support;
- detailed maps, comparisons, timelines and inspection panels;
- clear status, ownership and next-action language;
- editable media allocation with independent running periods;
- proposal-option comparison and controlled client decisions;
- delivery evidence linked to the exact booked media line.
- a persistent dark workspace shell, compact command headers, contiguous metric strips, tab rails,
  dense ledgers and split inspection panes;
- flat one-pixel operational surfaces instead of large marketing heroes, floating card collections,
  decorative pills, gradients, shadows or nested card blocks.

Advertified will not copy the prototype's parallel `Rapid OOH` and `Full Campaign` products, fabricated live metrics, UK-only providers, fixed proposal budgets, browser-owned commercial state, false certification language or client-side authoritative PDF generation.

## Canonical experience

One lifecycle remains authoritative:

`Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning`

Audience/STP, media mix, inventory, supplier confirmation and MediaPlanVersion are governed
sub-stages of Plan, not parallel lifecycles.

`OOH_ONLY` and `FULL_CAMPAIGN` use the same screens and commands. Campaign mode only controls the channels available to planning and is immutable once planning begins.

## Current implementation slice

This packet carries the authenticated product foundation into the current canonical delivery
workflow without inventing unavailable backend data:

1. Rebuild the application shell to the approved prototype composition with grouped navigation, compact workspace context and a prominent Brief entry action.
2. Replace the foundation-only home page with a real role-aware work dashboard using persisted opportunities, human tasks and workspace data.
3. Upgrade supplied-Brief intake into a source-first workbench that explains the retained source, AI interpretation and clarification boundary in human language.
4. Add reusable campaign-stage and evidence presentation components across Brief, Planning,
   Proposal, Booking and Delivery screens.
5. Upgrade the Brief review and Planning workspace hierarchy while preserving the existing APIs and business rules.
6. Keep the design market-neutral: no hard-coded market, supplier, measurement provider, currency symbol or tax label.
7. Extend the same workbench through funding, Booking, creative readiness, supplier proof,
   performance evidence and measurement review without creating parallel mini-applications.

## Delivered in this slice

- Professional authenticated shell with grouped, role-aware navigation, workspace context and a prominent source-first Brief action.
- Authenticated screen system uses the prototype's dense dark-slate composition, flat divider-based
  panels, rectangular statuses and compact controls; the existing public site remains isolated.
- Real work dashboard backed by persisted tenant, opportunity and human-task APIs; restricted queues degrade independently without fabricated counts.
- Source-first supplied-Brief experience: the system retains the original words, extracts structured details using authorised evidence, and decides `OOH_ONLY` or `FULL_CAMPAIGN`; a human is asked only to resolve materially unclear details.
- The API-owned, tenant-safe client name remains visible from Brief intake into the Brief and Planning workspaces. A user does not need to pre-register a client or work from a client identifier.
- Detailed Brief workspace with structured commercial sections, retained source, integrity reference and immutable version timeline.
- Reusable campaign progression across Brief, Planning, Proposal and Booking without introducing a second OOH workflow.
- Planning workbench header with the client-approved total, selected-supply and permitted objection status; section navigation; locked campaign-scope context; editable mix, timeline, inventory and plan stages.
- Browser-locale money and date presentation with explicit currency and tax values from persisted records.
- Desktop and compact responsive treatment with reduced-motion support and accessible navigation/controls.
- One connected Campaign Delivery workspace from accepted proposal through funding, Booking,
  creative readiness, launch/completion, proof, performance evidence and approved measurement.
- Lifecycle-aware Funding, Bookings, Creative, Live, Proof and Measurement tabs automatically open
  the server-persisted current stage while preserving stable deep links and manual review navigation.
- Role-scoped supplier proof-request and submission journeys whose actions remain server-authorised.
- Client-safe Booking and Planning projections: supplier cost/private notes and internal planning
  assumptions/objections are removed from client-facing responses.
- Strict Zod response validation on the newly integrated Campaign Delivery boundaries.
- OOH Proposal-inbox automation remains off until a tenant administrator explicitly opts in. The UI reports a provider submission as sent and never claims inbox delivery without provider evidence.
- Notifications are single, non-blocking status updates so concurrent work cannot be obscured or intercepted by stacked toasts.

## Acceptance evidence

- `npm --prefix web run lint`: PASS — zero warnings and errors across source and E2E files.
- `npm --prefix web run build`: PASS — TypeScript and Vite production build transformed 1,876
  modules; route-level lazy loading reduced the main JavaScript chunk from 653.65 kB to
  482.86 kB (137.56 kB gzip) and removed the oversized-chunk warning without raising the threshold.
- `npm --prefix web test`: PASS — 6 focused web boundary tests.
- `python -m pytest tests/architecture/test_boundaries.py tests/architecture/test_gate1_enforcement.py -q`:
  PASS — 23 architecture tests.
- Serial Playwright desktop: PASS — 16 journeys in the retained run.
- Serial Playwright compact at 390 × 844: PASS — 16 journeys in the retained run.
- The focused Campaign Delivery journey passed independently on desktop and compact.
- Normal configured Playwright: PASS — 32/32 across desktop and compact with four workers.
- No separate Rapid OOH route, aggregate or planning page was introduced.
- No generated operational counts, reach, ROI, availability, supplier response or certification claim was introduced.
- API response schemas remain strict and server-owned commercial actions remain authoritative.
- The working tree remains intentionally unstaged and uncommitted.
