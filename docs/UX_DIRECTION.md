# Advertified authenticated product UX direction

**Owner direction date:** 2026-08-31
**Visual references reviewed:** Omnicom Omni and the owner-supplied Google AI Commercial Media OS prototype.
**Boundary:** The owner-supplied Google AI prototype is the approved composition reference for the authenticated screens, including its persistent rail, compact command bar, visual density, metric strips, tabbed workspaces, ledgers and inspectors. Advertified must reproduce that screen character rather than merely borrowing its colours. The prototype is not business truth: do not copy its mock data, wording, browser-owned state, parallel workflows, fixed budgets, market assumptions or fabricated actions. Every screen remains bound to Advertified's evidence, lifecycle and authority rules.

## Experience objective

Advertified must be simple to operate without hiding the commercial, planning and supply detail required to make a sound decision. Each page leads with the task, current truth and next action; deeper technical detail is available without overwhelming the first view.

## Useful Omni patterns translated to Advertified

| Observed pattern | Advertified application |
|---|---|
| Central orchestration project/status list | Role-specific home queue showing real briefs, approvals, proposals, campaigns and exceptions |
| KPI cards plus trend chart and recommendations | Planning/measurement panels with dated metrics, evidence, assumptions and actions |
| Audience/image segment cards | Reviewed audience definitions and evidence classifications, never inferred personal sensitive traits |
| Budget scenario controls and allocation panels | Editable media mix plus one to three materially different proposal options with reconciled totals |
| Geographic heat/map panel | OOH inventory, POIs, coverage, routes and availability with source and freshness |
| Asset collage and format adaptation | Versioned creative asset/format matrix linked to approved campaign requirements and separate brand/supplier reviews |
| Six large solution cards | Small number of clear product/work areas, each with one purpose and one primary entry action |

## Professional workbench patterns adopted

- Persistent workspace context, grouped navigation and one prominent Brief entry action.
- Real work queues, recent activity and stage distribution instead of decorative dashboard numbers.
- Source-first Brief intake with clarification cards, structured review and retained evidence beside the decision.
- One visible canonical progression: Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning.
- Detailed planning headers, section navigation, editable allocation, timelines and inspection panels without creating separate OOH and full-campaign products.
- Proposal comparison and booking context carried forward through the same campaign progression.
- A dark slate operational canvas, navy navigation and restrained electric-blue actions provide the same connected media-workbench character as the approved prototype.
- Flat, contiguous metric strips, tabs, ledgers and split inspectors replace floating card collections, decorative pills and card-inside-card layouts.

## Page rules

- Use progressive disclosure: summary, comparison, evidence/detail, then action.
- Dense tools receive a dedicated page; do not force editing into cramped drawers or card carousels.
- One primary action per decision state. Secondary actions remain visible but visually quieter.
- Status, progress and activity reflect persisted backend truth only. Never manufacture percentages or “AI is thinking” theater.
- Tables handle exact values and operational lists; charts handle comparison, trend and composition; maps handle geography.
- Every chart shows title, unit, period, source/freshness and a usable nonvisual representation.
- Icons reinforce meaning and always have text or an accessible label.
- Animation explains transition, selection, loading or changed state. It is subtle, tokenised and disabled/reduced when the user requests reduced motion.
- Use skeletons only for genuine loading. Empty, forbidden, stale, failed and recovery states are designed explicitly.
- Currency display and input precision derive from governed currency metadata and locale-aware formatters; reusable UI never assumes one market or a fixed minor-unit scale.
- Supplier-private cost, notes, internal assumptions and internal objections are removed by the server projection for client-facing users, not merely hidden in the browser.
- Keep the existing Advertified identity and public site. The authenticated product uses neutral slate/navy rather than dark green.
- Authenticated operational surfaces use one-pixel dividers, no decorative shadows or gradients, compact rectangular status labels and radii no larger than eight pixels. Spacing, chart colours, icon sizes and animation timing come from design tokens.

## Validation and notification

- Zod validates forms, route/query parameters, browser storage and every API response before use.
- Inline validation sits beside the field and explains how to correct it.
- A single NotificationService owns Toastr-style notifications. Components never call its adapter directly.
- Toasts confirm nonblocking outcomes; they never hide blocking errors or replace persistent task state.

## Human-language firewall

The user sees the business issue and recovery action, not internal implementation language.

| Internal fact | Human-facing treatment |
|---|---|
| Tenant or permission denial | “You don’t have access to this workspace or item.” |
| Stale optimistic version | “This was changed by someone else. Review the latest version before continuing.” |
| Missing required Brief input | Name the missing business information and return focus to that section |
| Provider/database exception | Neutral failure and recovery action; optional support reference only |
| Agent/tool/job code | Business stage such as “Research needs review” or “Planning could not continue” |

Never render raw exceptions, SQL/provider messages, stack traces, prompts, private reasoning, internal event names, tool names or developer terminology.

## Implemented screen families

The authenticated product now contains gate-backed screen families for:

- opportunity/evidence intelligence;
- Brief review and version comparison;
- audience and media-mix analysis;
- inventory catalogue/detail/review and map;
- media plan, forecast and scenario comparison;
- proposal option comparison and versioned PDF preview;
- one connected Campaign Delivery workspace covering funding, Booking, creative readiness, explicit
  launch/completion, supplier proof, performance evidence and measurement review.

The screen implementation now follows the approved prototype composition across the shell, dashboard,
Brief intake/review, planning, proposal, inventory, opportunities, marketplace, funding, Booking,
Campaign Delivery, supplier proof, profile and administration surfaces. This approval applies to visual
composition only; the prototype's demo workflows and mock commercial state remain explicitly rejected.

These screens bind to canonical API data and server-authorised actions. Local release verification
and owner gate decisions remain separate from implementation status; no screen may be described as
production-ready merely because it renders.
