# Gate 3 work packet — authenticated application shell

## Status

**APPROVED FOR LOCAL NON-PRODUCTION IMPLEMENTATION — Brian Rabuthu, 2026-08-29**

Prepared after Brian Rabuthu directed the repository to move beyond the owner-approved
Gate 2 on 2026-08-29. Brian approved continuation with this exact packet on 2026-08-29.
Remote publication, production deployment, live Cognito, cloud mutation and external
communication remain prohibited.

## Outcome

A human user can enter the local authenticated React application, choose an authorised
workspace, see a tenant-correct role-aware shell backed only by real Gate 2 API data, update
their profile safely and sign out. The browser holds no provider or bearer token. Every page
has accessible loading, empty, forbidden, stale, failure and recovery behaviour.

## Governing sources

- Normative sections 8, 20, 21.1, 24 and the narrow task rule in 25.3;
- accepted ADR-0005 for BFF session, cookie, CSRF and service-identity separation;
- accepted ADR-0008 for Zod, NotificationService, React-Toastify and human-safe errors;
- `docs/UX_DIRECTION.md` for layout, visual truth and accessibility;
- generated `shared/contracts/openapi/advertified-commercial-api.v1.json` as the canonical
  browser/API contract.

## Approved local authentication boundary

- Add provider-neutral browser-session ports in the C# Application layer.
- Add a deterministic Development/Test session adapter only. It creates an opaque random
  session identifier and keeps identity/session state server-side in a bounded in-memory
  store. It cannot start or authenticate in Production.
- Use an `HttpOnly`, host-scoped, `SameSite=Lax` cookie. Local HTTP may use the development
  secure-cookie policy; Production configuration remains fail-closed and unimplemented.
- Add antiforgery token issuance, validation and same-origin enforcement to every unsafe
  cookie-authenticated request. Invalid tokens return safe ProblemDetails.
- Logout invalidates server state before clearing the cookie.
- React never receives or stores provider tokens, session identifiers, trusted permissions
  or provider claims.
- Live Cognito/OIDC, Redis session storage and production cookie/domain configuration remain
  out of scope. The ports must permit those later adapters without changing browser contracts.

## Proposed browser scope

### Real routes

| Route | Outcome | Real source |
|---|---|---|
| `/sign-in` | Start the deterministic local session | Development/Test session endpoint |
| `/workspaces` | Choose an active authorised organisation | `GET /api/v1/workspaces` |
| `/home` | Show workspace identity, relevant foundation records and next available action | Gate 2 tenant and permitted cursor queries |
| `/profile` | Review and update the signed-in user's profile | `GET /api/v1/me`, `PUT /api/v1/tenants/{tenantId}/me` |

### Truthful deferred routes

`/tasks` and `/notifications` may appear only as disabled destinations with a plain reason.
There is no HumanTask, notification or later-gate workflow source yet, so Gate 3 must not
create mock queue entries, dashboard KPIs, progress, approvals or notification counts.
The first gate that owns a real workflow must add the persisted task/read model before these
routes can be claimed implemented.

### Shell behaviour

- Desktop navigation, compact small-screen navigation and a workspace switcher.
- Server-returned role and permission outcomes control presentation; the browser never
  becomes authorization authority.
- Unsupported later-gate destinations are disabled with a reason, not linked to mock pages.
- One primary action per state and progressive disclosure for technical support references.
- Advertified navy, white, neutral grey and electric blue tokens; no dark green.
- Meaningful icons include text or accessible names. Motion is subtle and disabled under
  reduced-motion preferences.

## Approved browser contracts and dependencies

- Zod 4 for route, query, storage, form and every consumed API response.
- React Router for real client-side routes and guarded navigation.
- React-Toastify behind one `NotificationService`; no component imports the adapter.
- Playwright for one authenticated acceptance journey at the approved desktop and compact
  viewports.
- A focused API client that sends correlation, antiforgery, idempotency and `If-Match`
  headers where the OpenAPI contract requires them and parses safe ProblemDetails.

All packages must be locked to reviewed versions. No chart, map, state-management or icon
library is added unless an implemented screen demonstrates a real need.

## API changes in scope

- deterministic local session start, session status/antiforgery and logout endpoints;
- cookie-authenticated resolution into the existing canonical `ICurrentIdentity` boundary;
- safe expired-session and invalid-CSRF problems;
- no startup migration and no new canonical business aggregate;
- only additive OpenAPI changes required by the browser session boundary.

No new database migration is proposed. The deterministic session store is process-local and
contains no production or provider credential. Production session persistence requires a
separate reviewed adapter and deployment decision.

## Explicitly out of scope

- live Cognito/OIDC resources, SDK calls, credentials or provider configuration;
- production session storage, domains, certificates, CORS origins or deployment;
- invitations, access administration or membership mutation;
- HumanTask/notification persistence and fabricated work queues;
- Opportunity, Brief, inventory, planning, proposal, campaign or supplier screens;
- commercial dashboard KPIs whose source aggregates do not exist;
- live AI, email, maps, analytics providers or external effects.

## Smallest implementation sequence

1. Implement and verify the provider-neutral session contract and deterministic local adapter.
2. Add cookie, antiforgery, same-origin, expiry and logout API behaviour.
3. Regenerate OpenAPI and add the focused TypeScript client plus Zod response schemas.
4. Add route guard, workspace selection and tenant-safe shell.
5. Implement `/home` from permitted Gate 2 reads and `/profile` with ETag/idempotent update.
6. Add the NotificationService boundary and only the notifications exercised by real actions.
7. Add one focused Playwright journey and complete accessibility/state verification.
8. Retain Gate 3 evidence and request the owner GO.

## Acceptance evidence

| Risk | Minimum evidence |
|---|---|
| Authentication | Unauthenticated, expired, invalid-cookie and invalid-service-identity denial |
| CSRF/session | Missing/invalid antiforgery denial, same-origin check, logout invalidation and no browser token storage |
| Tenancy | Wrong-tenant route/API denial and workspace switching cannot retain stale tenant authority |
| Browser validation | Representative malformed route, storage, form, success and ProblemDetails payloads fail safely |
| Concurrency/retry | Profile update sends ETag/If-Match and idempotency; stale and duplicate outcomes recover safely |
| Human language | Stable problem codes map to plain recovery wording; raw server content is never rendered |
| Accessibility | Keyboard journey, focus recovery, semantic landmarks, toast announcement and reduced motion |
| Responsive shell | Same real journey works at 1280px desktop and a compact mobile viewport |
| Truth | No mock task, KPI, approval, notification, provider call or later-gate record appears |
| Architecture | Dependency, line, complexity, adapter-isolation and notification-import rules remain green |

Only acceptance-critical cases are added. Do not test React, Zod, React-Toastify,
Playwright or ASP.NET Core framework behaviour itself.

## Recovery

- Removing the deterministic session adapter returns the API to the Gate 2 authentication
  boundary without schema rollback.
- The authenticated page replacement was retained only after the desktop and compact
  journeys passed; the clean parent and Gate 2 evidence remain rollback references.
- No destructive data operation, provider call or production resource is part of this packet.

## Authorisation record

Brian Rabuthu approved this exact packet, including:

1. the deterministic process-local session adapter for local Gate 3 only;
2. React Router, Zod 4, React-Toastify and Playwright as locked dependencies;
3. the truthful deferral of real task, notification and unsupported KPI data until an owning
   workflow gate creates their canonical sources.

Approval of this packet authorises local reversible implementation only. It does not
authorise a commit, push, merge, deployment, live provider, cloud resource or production use.

## Completion boundary

The bounded packet is implemented and verified locally as recorded in
`docs/evidence/gate-3/`. Brian Rabuthu recorded the dated Gate 3 completion GO on
2026-08-29. Gate 4 remains blocked until its exact work packet is separately approved.
