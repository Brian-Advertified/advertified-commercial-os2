# Advertified authenticated product UX direction

**Owner direction date:** 2026-08-29
**Visual reference reviewed:** [Omnicom Omni — Omni Solutions](https://www.omc.com/omni/)
**Boundary:** Use interaction and information-design patterns only. Do not copy Omnicom branding, imagery, wording, data or proprietary screens.

## Experience objective

Advertified must be simple to operate without hiding the commercial, planning and supply detail required to make a sound decision. Each page leads with the task, current truth and next action; deeper technical detail is available without overwhelming the first view.

## Useful Omni patterns translated to Advertified

| Observed pattern | Advertified application |
|---|---|
| Central orchestration project/status list | Role-specific home queue showing real briefs, approvals, proposals, campaigns and exceptions |
| KPI cards plus trend chart and recommendations | Planning/measurement panels with dated metrics, evidence, assumptions and actions |
| Audience/image segment cards | Reviewed audience definitions and evidence classifications, never inferred personal sensitive traits |
| Budget scenario controls and allocation panels | Media-mix scenario comparison and three proposal tiers with reconciled totals |
| Geographic heat/map panel | OOH inventory, POIs, coverage, routes and availability with source and freshness |
| Asset collage and format adaptation | Later creative asset/format matrix linked to approved campaign requirements |
| Six large solution cards | Small number of clear product/work areas, each with one purpose and one primary entry action |

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
- Keep the existing Advertified visual identity. Do not introduce dark green. Dark analytical sections may be used sparingly with neutral black/charcoal and the approved accent tokens.
- Radii, shadows, spacing, chart colours, icon sizes and animation timing come from design tokens, not one-off values.

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

## Screen families

Gate 3 establishes only the authenticated shell, role home, work queue and real API states. Later gates add:

- opportunity/evidence intelligence;
- Brief review and version comparison;
- audience and media-mix analysis;
- inventory catalogue/detail/review and map;
- media plan, forecast and scenario comparison;
- proposal tier comparison and PDF preview;
- campaign delivery, proof and measurement.

No later screen is mocked as implemented before its gate owns real data and actions.
