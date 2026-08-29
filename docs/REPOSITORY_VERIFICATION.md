# Repository verification

**Repository:** `C:\Users\CC KEMPTON\source\advertified-commercial-os2`  
**Branch:** `master`  
**Verification date:** 2026-08-29  
**State:** Gate 3 locally verified; expected uncommitted owner-review changes

## Clean parent

Before remediation, repository status reported a clean `master` worktree. The previously recorded HEAD was:

```text
0986f62ad0748289fdafe8f36f5f9a3dabaab4d8
```

That commit remains the immutable parent/rollback reference. Re-run `git rev-parse HEAD` immediately before any owner-authorised commit.

## Verified local results

| Check | Result |
|---|---|
| Normative specification integrity | Seven of seven split-part SHA-256 hashes match source chunks |
| React exact runtime version | 19.2.0 |
| Web lock/install | package lock generated; install succeeded |
| Web lint/type-check/tests/build | PASS / PASS / 4 PASS / PASS |
| Authenticated desktop/compact browser journeys | 4 PASS |
| Web runtime dependency audit | 0 vulnerabilities |
| .NET target at Gate 0 | net8.0 (historical; superseded by accepted ADR-0009) |
| API Release build/tests | PASS with zero warnings / 32 PASS |
| Python runtime baseline | 3 tests PASS; provider disabled |
| Architecture tests | 21 PASS |
| Compose validation/build/up | PASS |
| os2 services | four healthy |
| PostgreSQL | 16; health requires pgcrypto, PostGIS, pgvector |

## Local Docker isolation

The new project uses loopback-only ports 55432, 56379, 59000/59001, and 51025/58025 to avoid silently using containers from other Advertified workspaces. No Docker volume was deleted. Only containers carrying the os2 Compose project label count as evidence.

## Not performed

- no commit, stage, push, pull, merge, or deploy;
- no AWS/cloud mutation;
- no production data or secret retrieval;
- no live or paid model/provider call;
- no Gate 4 or later product implementation;
- no remote GitHub Actions run.

See `docs/GATE0_VERIFICATION_STATUS.md` and `docs/CAPABILITY_LEDGER.md` for the controlling status.
Current .NET 10/C# 14, Gate 2 and Gate 3 evidence is retained separately under
`docs/evidence/gate-2/` and `docs/evidence/gate-3/`; the clean parent remains the rollback
reference rather than evidence of the later implementation.
