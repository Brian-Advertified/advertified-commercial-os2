# Gate 12 critical-journey accessibility work packet

**Status:** IMPLEMENTED LOCALLY — certification remains incomplete  
**Owner direction:** continue production hardening without unnecessary containers, images, tests or live-provider spend  
**Date:** 2026-09-01  
**Normative source:** `docs/spec/02-experience-architecture-and-decisions.md` and `docs/spec/06-integrations-operations-and-certification.md`

## Bounded requirement

Verify and correct accessibility behavior that can be exercised with the existing Playwright stack on the current connected application. This packet covers keyboard navigation, skip-to-content behavior, focus placement, named landmarks and screen-reader-facing accessible names on the sign-in and authenticated critical shell. It must not introduce a parallel test framework or claim a full WCAG 2.2 AA audit from semantic smoke checks alone.

## Acceptance evidence

- Sign-in exposes one main landmark, a labelled primary action and a logical keyboard target.
- The authenticated shell exposes a labelled primary-navigation landmark and one main content landmark.
- Authenticated SPA route changes move focus to `#main-content` so keyboard and screen-reader users receive the new page context instead of inheriting stale focus.
- On a cold authenticated page load, the existing `Skip to main content` link is the first keyboard target, becomes visible on focus and transfers focus to `#main-content` when activated.
- Critical actions used by the connected Brief/proposal path have accessible role/name projections rather than relying only on visual icons.
- The focused connected accessibility regression passes without mocked API traffic or new Docker resources.
- Existing connected critical journeys and architecture checks remain green.

## Explicit remaining certification boundary

A full automated WCAG 2.2 AA engine result plus named manual keyboard/screen-reader review across the complete critical-journey matrix remains required before production certification. This local packet must report that boundary instead of declaring accessibility complete.
