# ADR-0004: Stack Version Deviation Rectification

## Status
Accepted

## Context
During repository verification, version deviations from the specification were discovered:

**Specification baseline** (Section 17):
- React 19.2.0
- .NET 8.0
- TypeScript not specified in baseline
- Vite not specified in baseline

**Repository state** (OBSERVED):
- React 19.2.8 (deviation from specification)
- .NET 8.0 (matches specification)
- TypeScript 6.0.2 (not in specification baseline)
- Vite 8.2.2 (not in specification baseline)

## Decision
**Align repository with specification baseline and document acceptable deviations.**

### Version Corrections

**Immediate corrections required**:
- React: 19.2.8 → 19.2.0 (exact specification match)
- .NET 8.0: No change (matches specification)

**Acceptable deviations to be documented**:
- TypeScript 6.0.2: Latest stable for React 19.2.0 compatibility
- Vite 8.2.2: Latest stable for React 19.2.0 compatibility

### Rationale
1. **Specification authority**: Section 17 explicitly states React 19.2.0 as locked baseline
2. **Semver compatibility**: Minor version deviations (19.2.0 → 19.2.8) are semver-compatible but should be explicit
3. **Toolchain consistency**: TypeScript and Vite versions are toolchain choices not specified in baseline
4. **Future proofing**: Document acceptable deviations to prevent silent drift

## Version Policy

### Locked Baseline (No Deviation Without ADR)
- React 19.2.0 (exact match required)
- .NET 8.0 (exact match required)
- PostgreSQL with PostGIS and pgvector (exact extension versions)

### Acceptable Deviations (Documented)
- TypeScript: Latest stable compatible with React 19.2.0
- Vite: Latest stable compatible with React 19.2.0
- Python: 3.10+ (specification minimum, actual version documented)
- Node.js: 20+ (specification minimum, actual version documented)

### Version Documentation Requirements
For every dependency, record:
- Specification baseline (if specified)
- Installed version
- Whether exact or semver-compatible versions are permitted
- Support status
- Reason for deviation (if any)
- Verification result
- Approving ADR (if deviation)

## Implementation

1. **Immediate actions**:
   - Correct React version to 19.2.0 in package.json
   - Document TypeScript and Vite versions as acceptable deviations
   - Create version registry in implementation plan

2. **Ongoing requirements**:
   - CI pipeline checks for specification baseline compliance
   - ADR required for any specification baseline deviation
   - Version registry maintained and updated

## References
- Advertified Unified Specification v1.1, Section 17 (Technology baseline)
- React 19.2.0 documentation
- .NET 8.0 documentation

## Participants
- System Architect - Technical decision
- Engineering Lead - Implementation verification

## Date
2026-08-29