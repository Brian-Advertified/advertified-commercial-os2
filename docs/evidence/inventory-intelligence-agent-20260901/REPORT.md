# Inventory Intelligence agent verification — 2026-09-01

## Scope

Complete the missing `inventory_intelligence` interpretation boundary without moving inventory truth, eligibility, rate, availability or benchmark calculations out of the Commercial API. No live provider, paid AI call, external communication or new Docker project was permitted.

## Implemented result

- Deterministic mode now exposes all eleven approved agent handlers.
- Inventory Intelligence receives the exact BriefVersion and InventoryShortlistVersion plus governed candidate facts only.
- The Python runtime produces explanation text without changing eligibility, scores, rates, currencies, benchmark values or IDs.
- The C# planning boundary validates candidate coverage and binds the explanation to the existing shortlist recommendation data.
- The shortlist UI displays the explanation alongside deterministic benchmark/rejection information.
- The connected proposal journey now asserts that the Inventory Intelligence rationale is visible before selection and then completes plan approval, proposal approval, PDF rendering and client share.

## Verification observed

- Runtime: `python -m pytest agent-runtime -q` — PASS, 31/31.
- Master data: `npm --prefix web run master-data:check` — PASS, registry 2.12.0.
- Web lint/type/unit — PASS, unit 6/6.
- Web production build — PASS. Explicit Vite 8 vendor splitting removes the Linux-only oversized-main-chunk regression without changing the warning threshold.
- Pinned Linux images — PASS for API, migrator, runtime and web. The API publishes with .NET SDK 10.0.400.
- Current `advertified-dev` stack — PASS; migration/bootstrap/seed jobs completed and API/runtime/web are healthy.
- Connected Inventory Intelligence proposal — PASS, 1/1 with rationale assertion.
- Complete connected local critical journeys — PASS, 3/3.
- Final-tree architecture — PASS, 42/42.

## Current limitation

The complete current-source C# test suite has not been rerun after these latest changes. `global.json` requires .NET SDK 10.0.400 with roll-forward disabled, while this Windows host has 10.0.103. The current API still compiles and publishes successfully inside the pinned 10.0.400 Linux image, and the connected journeys exercise the rebuilt API. A 10.0.400-capable test runner or remote CI remains required before the complete C# result can be called current.

## Resource and cost discipline

The existing `advertified-dev` Compose project and canonical image names were reused. No new Compose project, live provider, production resource or paid model was used. Incremental AI cost is zero.
