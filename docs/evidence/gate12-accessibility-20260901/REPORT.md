# Gate 12 critical-journey accessibility — 2026-09-01

## Implemented correction

The authenticated shell now manages focus on SPA route changes. When the route changes inside the mounted application shell, focus moves to the existing `#main-content` landmark. This prevents keyboard and screen-reader users from inheriting stale focus on a control from the previous page. Initial page load does not steal focus, so the existing skip link remains available as the first keyboard target.

## Connected verification

The focused connected accessibility journey passes 1/1 against the packaged local web/API/database stack. It proves:

- one main landmark on sign-in and authenticated pages;
- named primary navigation and critical controls through the browser accessibility tree;
- route-change focus transfer to main content;
- cold-load keyboard access to the visible-on-focus skip link and successful skip activation;
- labelled Brief title/source controls and the `Understand this Brief` action.

The complete connected local set then passes 4/4, including proposal inbox, clear Brief to OOH planning and Brief through approved PDF/share. Final-tree architecture passes 42/42. The current pinned Linux web image builds successfully and retains the corrected Vite chunk split.

## Certification boundary

This packet does not claim WCAG 2.2 AA certification. A standards-based automated WCAG engine scan and named manual keyboard/screen-reader review across the complete critical-journey matrix remain required in production-shaped staging before launch.

No new Docker project, live provider, production resource or paid model was used.
