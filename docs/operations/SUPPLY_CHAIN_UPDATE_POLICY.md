# Application supply-chain update policy

**Status:** implemented for local/CI enforcement; production signing and release approval remain blocked  
**Effective date:** 2026-09-01

## Controlled inputs

- Application dependencies remain in the committed NuGet, npm and Python hash lockfiles.
- CI actions use full commit SHAs; application and scanner base images use versioned tags plus
  manifest digests.
- The exact .NET, Node and Python build versions are recorded in repository build inputs.
- Dependency, action, base-image or scanner changes require a bounded review; automatic merge is
  forbidden.

## Required update evidence

Every proposed update must retain:

1. the old and new immutable references and lockfile diff;
2. affected architecture, build, unit, contract and process-smoke outcomes;
3. a CycloneDX SBOM for each final API, migrator, agent-runtime and web image;
4. a pinned Trivy scan of each final-image SBOM that fails on HIGH or CRITICAL findings;
5. an explanation of changed transitive dependencies, runtime behavior and rollback; and
6. named owner review plus the required Security/Operations review before deployment.

The runtime-only npm audit and the full build dependency audit are separate required signals because
build tools execute while producing the trusted browser artifact. Python installs continue to use
`--require-hashes`; .NET restores continue to use locked mode.

## Exceptions and response

A vulnerability is not waived by confidence, lack of an exploit demonstration, or a passing unit
test. A temporary VEX or risk exception must identify the exact image/component/CVE, affected
version, evidence, compensating control, expiry, remediation owner and authorised Security decision.
Expired or unapproved exceptions fail the release.

New advisories and release candidates trigger the same scan and review. A compromised or withdrawn
tool/image version is removed from allowed inputs and replaced through this process; historical
evidence remains immutable. The known compromised Trivy `0.69.4` release is explicitly forbidden.

## Release boundary

Dirty-tree images and local scan files are verification evidence, not release artifacts. Signing,
provenance attestation, registry publication and deployment require a clean owner-authorised commit,
an approved registry identity, immutable image digests, retained clean-CI reports and the named
Gate 12/13 decisions. No local agent may approve those decisions.
