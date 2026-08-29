# ADR-0004: Stack version alignment

## Status

Superseded by the normative specification, exact manifests, and verified Gate 0 baseline.

## Recorded outcome

- React and React DOM are exactly 19.2.0.
- Commercial API targets .NET 8.
- TypeScript is exactly 6.0.2.
- Vite is exactly 8.2.2.
- Local database is PostgreSQL 16 with PostGIS and pgvector 0.8.6.
- Python runtime is 3.12-compatible.
- Node development/CI baseline is 22.

Dependency changes require a bounded update, compatibility evidence, lockfile diff, affected builds/tests, and an ADR when they alter a locked architecture decision. “Latest” is not a version policy.

This ADR grants no approval for future version drift.
