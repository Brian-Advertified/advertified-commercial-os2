# Repository verification

**Repository:** `C:\Users\CC KEMPTON\source\advertified-commercial-os2`  
**Branch:** `master`  
**Verification date:** 2026-08-29  
**State:** expected uncommitted remediation changes

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
| Web lint/type-check/tests/build | PASS / PASS / 2 PASS / PASS |
| .NET target | net8.0 |
| API Release build/tests | PASS with zero warnings / 2 PASS |
| Python runtime baseline | 3 tests PASS; provider disabled |
| Architecture tests | 10 PASS |
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
- no product-feature implementation;
- no remote GitHub Actions run.

See `docs/GATE0_VERIFICATION_STATUS.md` and `docs/CAPABILITY_LEDGER.md` for the controlling status.
