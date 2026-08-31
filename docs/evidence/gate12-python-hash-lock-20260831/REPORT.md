# Gate 12 Python hash-lock evidence

**Evidence date:** `2026-08-31`

**Base commit:** `27068aef7917d971db11edeb153b27ddb5b6104a`

**Live provider or production resource used:** No

## Implemented

- Added separate generated runtime and development lockfiles. The runtime graph contains 19 exact
  package versions and the development graph contains 58, with accepted distribution hashes for
  every installable requirement.
- Pinned pip-tools 7.6.1 as the generator. Stable generated headers identify the platform-neutral
  `python -m piptools compile` command, while the retained generation options require hashes, retain
  unsafe build-tool requirements and use the backtracking resolver.
- Changed the agent-runtime CI job to install `requirements-dev.lock` with `--require-hashes` and to
  key its dependency cache from that lock.
- Added a CI regeneration check for both lockfiles. CI fails when resolving either direct input
  changes the retained graph, and audits both locked graphs before lint and tests.

## Verification

| Check | Result |
|---|---|
| Clean hash-verified installation | PASS - a previously absent ignored target installed the complete development graph with `--require-hashes` |
| Locked runtime versions | PASS - FastAPI 0.141.1, Pydantic 2.13.4, Starlette 1.3.1 and pytest 9.1.1 imported from the isolated target |
| Deterministic Python checks | PASS - Ruff and 28/28 tests against the isolated locked graph |
| Runtime lock audit | PASS - no known vulnerabilities found |
| Development lock audit | PASS - no known vulnerabilities found |
| Deterministic regeneration | PASS - both SHA-256 file digests were unchanged after exact regeneration |
| CI lock gate | IMPLEMENTED - hash install, regeneration diff and both audits precede lint/tests; remote execution remains pending |
| Complete architecture suite | BLOCKED - 21/23; separately owned screen work contains a 521-line CSS file and inline governed `PENDING` |
| Web/browser regression | NOT RUN - another agent owns and is actively changing the screen tree |

The retained runtime lock digest is
`E4F33B5558665BB7CA2D1B7A20BAC9E45E606F21229F290C513731CBBC344374`; the development lock digest
is `1E41A30BDDD30192E51A0AE94549CC1573001EE00FD776CEC35A2A5FF99D2BA7`.

## Security boundary

Hashes prevent an installer from accepting distribution bytes not listed by the reviewed lock, and
exact transitive versions prevent resolver drift. They do not establish package provenance, software
composition policy or permanent vulnerability clearance. CI must continue to regenerate and audit
the graphs on every change.

Container/base-image scanning, SBOM generation, signing/provenance, secret scanning, dependency
update policy and an independent security review remain required. Remote CI has not executed this
commit. No screen file, production resource, live/paid provider, push or deployment was used.
