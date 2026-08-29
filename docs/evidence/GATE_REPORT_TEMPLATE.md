# Gate N evidence report

**Evidence date:** YYYY-MM-DD  
**Repository/branch:** repository / branch  
**Base commit:** exact SHA or not yet recorded  
**Working tree:** clean, staged review, or uncommitted review  
**Decision:** PENDING — only the named owner may record GO

## Authorised outcome

State the exact gate outcome and specification sections. List the explicit in-scope and out-of-scope boundaries.

## Changes

| Path | Created/changed | Capability | Data impact |
|---|---|---|---|
| `path` | Created | What now exists | None or exact migration impact |

Do not call a scaffold implemented or an implementation verified without repeatable evidence.

## Verification

| Check | Exact command | Outcome | Retained evidence |
|---|---|---|---|
| Named check | `command` | PASS/FAIL/BLOCKED/PENDING | path or observed output |

List every failing or skipped check explicitly. “No tests discovered” is not PASS.

## Safety and boundaries

- Cross-tenant negative result:
- Permission-denial result:
- Migration/rollback result:
- Live or paid provider used: No
- Incremental AI cost: 0 minor units
- Production resource used or changed: No
- Secrets or production data introduced: No
- Consequential external action performed: No

## Unresolved blockers

For each blocker record the exact issue, named decision owner, smallest decision needed, safe work that may continue, and required retest.

## Diff and review

- Unrelated user changes preserved:
- Complete diff inspected:
- Files staged:
- Commit/push/deploy performed:
- Accountable owner:
- Required reviewers:
- Owner decision/date:

An AI may prepare this report but cannot approve the gate, security, privacy, legal compliance, or production readiness.
