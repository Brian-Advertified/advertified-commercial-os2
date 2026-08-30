# Gate 11 campaign booking-readiness evidence report

**Evidence date:** 2026-08-30

**Base commit:** `5f7655c354c4837d071ad34a3536f18d3ed93cbc`

**Live provider or production resource used:** No

## Implemented

- Added the canonical Campaign record and tenant-scoped list, detail and `ConfirmBookings` routes
  in the C# Commercial API.
- Made confirmed payment create exactly one `PLANNED` Campaign bound to the selected ProposalOption,
  ProposalDecision and approved MediaPlanVersion.
- Required confirmed funding and that planned Campaign before any selected-option booking can be
  created.
- Required every exact MediaPlanLine to have a confirmed booking before an authorised buyer-side
  command can advance the Campaign from `PLANNED` to `BOOKED`.
- Added separate correlated Campaign audit and outbox consequences to payment confirmation and
  booking-readiness confirmation, including durable idempotent replay of multiple consequences.
- Added forced RLS, immutable source snapshots, database lifecycle triggers, optimistic concurrency
  and governed Campaign permissions, resources, actions and event types.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS - zero warnings/errors |
| Complete C# suite | PASS - 64/64 |
| Campaign and booking journey | PASS - confirmed payment creates `PLANNED`; full confirmed booking coverage permits `BOOKED` |
| Negative boundaries | PASS - pre-funding booking, pending booking, repeat transition, supplier Campaign access and direct invalid mutation fail closed |
| Migration and forced RLS | PASS - all 72 protected tables verified in disposable PostgreSQL |
| Retained OpenAPI | PASS - generated contract matches the running API |
| Architecture | PASS - 23/23 |
| Agent runtime | PASS - Ruff and 26/26 |
| Web regression | PASS - master-data check, lint/build, 4/4 unit and 24/24 desktop/compact journeys |
| Compose validation | PASS |

The database independently reconciles Campaign creation to confirmed payment, the selected proposal
and option, the approved plan, delivery dates, owner and measurement snapshot. It independently
rejects booking before funding and rejects Campaign booking confirmation while any selected plan
line lacks a confirmed booking.

## Corrected findings

1. Booking was previously possible immediately after client selection. It now requires the canonical
   confirmed funding consequence and planned Campaign.
2. Payment confirmation needed two separately attributable state consequences. The command outcome
   store now persists and replays multiple same-command audit and outbox records atomically.
3. Application checks alone would leave database shortcuts. PostgreSQL now enforces both the
   funding-to-booking order and the exact `PLANNED` to `BOOKED` Campaign guard.
4. Campaign query logic initially used one governed status literal. The architecture check found it;
   the query now uses the generated master-data code and all architecture checks pass.

## Remaining boundary

This packet does not implement creative requirements or approval, readiness, campaign launch,
delivery completion, proof, measurement, reporting or learning. It does not contact suppliers or
clients, collect money, invoke a live provider, push, deploy or mutate production. Owner approval and
independent finance, legal, security/privacy and operations review remain pending.
