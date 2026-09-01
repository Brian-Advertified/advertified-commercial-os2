# Public-site integration work packet

**Owner direction:** bring only the public pages from
`C:\Users\CC KEMPTON\source\advertified-commercial-os` into this repository and keep the existing
authenticated application at `/sign-in` fully intact.

**Source inspected:** `redevelopment/advertified-clean` at `199ae88`, which matches that repository's
`main` and `origin/main` commit but also contains intentional uncommitted public-site redevelopment
changes. The source repository remains read-only.

**Current target base:** local `master` at `53209e7`. Separately owned uncommitted authenticated
screen work is present and must not be overwritten, staged or claimed by this packet.

## Bounded requirement

Add the source marketing pages, public navigation, brand assets, responsive styles, SEO metadata and
cookie-preference surface to this React application. Public routes must coexist with the canonical
authenticated routes and use this repository's router and session boundary.

## Acceptance evidence

1. `/` and every declared public route render through React Router without authentication.
2. `/sign-in` and every existing authenticated route retain the current session, workspace, tenant
   and permission boundaries.
3. Every old `/login` link points to `/sign-in`; campaign-start actions use a validated local return
   path to the canonical `/briefs/new` journey.
4. Public inventory and onboarding states do not fabricate data or success where this repository has
   no public API contract. Unavailable actions explain the real next step.
5. Public assets are repository-local and no source secret, environment file, API implementation,
   database model, deployment configuration or legacy authenticated module is copied.
6. The target passes architecture, lint, type-check, unit, production build and focused desktop/mobile
   Playwright coverage for public navigation and the public-to-sign-in handoff.
7. The target development server responds on `http://localhost:5173/` and
   `http://localhost:5173/sign-in`.

## Explicitly out of scope

- importing the source authenticated application, Commercial API, Python runtime or database;
- importing source Rapid OOH aggregates, routes, permissions or lifecycle concepts;
- inventing public inventory counts, onboarding acceptance, client records or campaign state;
- changing the other agent's authenticated screen design;
- committing, pushing, deploying, calling a live provider or mutating production.

## Retained verification evidence — 2026-08-31

- `python -m pytest tests/architecture -q` — 23 passed.
- `npm --prefix web run type-check` — passed.
- `npm --prefix web run lint` — passed.
- `npm --prefix web test` — 6 passed.
- `npm --prefix web run build` — passed; Vite transformed 1,876 modules. Route-level lazy loading
  reduced the main JavaScript chunk from 653.65 kB to 482.86 kB (137.56 kB gzip), and Vite reports
  no oversized chunk warning. The public route boundary is 64.42 kB and the authenticated shell
  boundary is 6.20 kB before their individual page chunks load.
- `npm --prefix web audit --omit=dev` — 0 vulnerabilities.
- Complete serial Playwright passed 16/16 on desktop and 16/16 on compact. The public-site file
  contributed all three tests in each project with no skipped route-catalogue coverage.
- Normal configured `npm --prefix web run test:e2e` passed 32/32 with four workers, confirming the
  lazy public routes and authenticated sign-in/workspace boundary under the CI-style execution mode.
- HTTP probes against the running local services — `/`, `/sign-in`, `/api/v1/session` and the API
  liveness endpoint each returned 200.
- A headless browser against `http://localhost:5173` rendered both the public home page and sign-in,
  completed deterministic local sign-in, and received exactly one `Advertified Local Development`
  workspace with the `platform_admin` role.

All provider switches remained local/off and no production or live AI/provider resource was used.
