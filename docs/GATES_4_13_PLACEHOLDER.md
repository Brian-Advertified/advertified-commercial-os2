# Gates 4-13: Detailed Implementation Plan - PLACEHOLDER

**Status**: INCOMPLETE - Stubbed out in main implementation plan

## Current Status

The main implementation plan (IMPLEMENTATION_PLAN.md) provides detailed templates for Gates 0-3 but stubs out Gates 4-13 with "[Continuing with remaining gates in same structured format...]". This document acknowledges that gap and provides the structure for completing those gates.

## Missing Gate Details

The following gates require completion using the same structured template as Gates 0-3:

### Gate 4: Evidence and Opportunity
**Status**: Not detailed
**Required detail**: Complete agent specifications for business_interpretation, strategy, opportunity_intelligence with full containment rules, test fixtures, and evidence requirements.

### Gate 5: Canonical Brief
**Status**: Not detailed
**Required detail**: CampaignBrief aggregate specification, BriefVersion immutability implementation, comparison UI, unknowns tracking, approval workflow.

### Gate 6: Inventory Truth
**Status**: Not detailed
**Required detail**: Complete ingestion pipeline (upload → quarantine → classify → render → extract → normalize → evidence-link → validate → review → publish → evaluate), document class handling, precision/recall thresholds.

### Gate 7: Planning
**Status**: Not detailed
**Required detail**: Audience agent, media mix agent, eligibility engine, shortlist generation, benchmark calculations, supply forecast integration, commercial truth handling.

### Gate 8: Proposal
**Status**: Not detailed
**Required detail**: Tier configuration, proposal narrative agent, financial calculations (VAT, fees, commission, markups), document generation, approval workflow.

### Gate 9: Rapid OOH
**Status**: Not detailed
**Required detail**: Automatic path detection, geography resolution, route/POI handling, OOH-specific eligibility, supplier confirmation, recalculation logic, scenario binding to Takealot Black Friday fixture.

### Gate 10: Supplier Marketplace
**Status**: Not detailed
**Required detail**: Supplier user management, listing creation, freshness tracking, RFQ system, supplier response workflow, booking management, commercial settings, ownership rules.

### Gate 11: Campaign Delivery
**Status**: Not detailed
**Required detail**: Creative workflow, booking management, proof submission, performance tracking, measurement agent, client reporting, campaign lifecycle.

### Gate 12: Hardening
**Status**: Not detailed
**Required detail**: Recovery mechanisms, security controls (certification, not introduction), POPIA compliance verification, performance optimization, observability, backup/restore, runbooks.

### Gate 13: Production Launch
**Status**: Not detailed
**Required detail**: Thirty-case certification, zero-Bedrock verification, unanimous greenlight process, AWS af-south-1 deployment, monitoring setup, handover documentation.

## Template for Gate Completion

Each gate should follow this structure:

**Gate ID**: Stable identifier (G4, G5, G6, etc.)
**Outcome**: User-visible result (specific, measurable)
**Included requirements**: Exact specification IDs (e.g., "Section 6.1, lines 45-67")
**Preconditions**: Verified dependencies (specific gates, specific evidence)
**Domain work**: Aggregates, rules, invariants (specific domain objects, specific invariants)
**Data work**: Migrations, seeds, indexes (specific table changes, specific indexes)
**API work**: Commands, queries, schemas (specific endpoints, specific schemas)
**Agent work**: Tools, policies, evaluations (specific agent configurations, specific evaluation fixtures)
**UI work**: Routes and complete states (specific routes, specific state behaviors)
**Integration work**: Adapters, failure behaviour (specific integrations, specific failure handling)
**Security**: Gate-specific controls (specific security measures)
**Tests**: Unit, contract, integration, Playwright (specific test types, specific coverage)
**Fixtures**: Named Advertified cases (specific fixtures from Section 31.2)
**Evidence**: Commands, artefacts, traces, screenshots (specific evidence requirements)
**No-go conditions**: Failures that block advancement (specific failure conditions)
**Owner approval**: Named accountable reviewer (specific person/role)
**Status**: Not started, active, blocked, verified
**Verdict**: GO/NO-GO with evidence

## Priority for Completion

Given the dependency chain, the gates should be completed in order:

1. **Gate 4** (Evidence and Opportunity) - Foundation for agent system
2. **Gate 5** (Canonical Brief) - Core commercial workflow
3. **Gate 6** (Inventory Truth) - Can progress in parallel with Gates 4-5
4. **Gate 7** (Planning) - Depends on Gate 5
5. **Gate 8** (Proposal) - Depends on Gate 7
6. **Gate 9** (Rapid OOH) - Depends on Gate 8
7. **Gate 10** (Supplier Marketplace) - Depends on Gate 6
8. **Gate 11** (Campaign Delivery) - Depends on Gate 8
9. **Gate 12** (Hardening) - Depends on all previous gates
10. **Gate 13** (Production Launch) - Depends on Gate 12

## Next Steps

1. Complete Gate 4 with full agent specifications and containment rules
2. Complete Gate 5 with aggregate specifications and approval workflows
3. Complete Gate 6 with complete ingestion pipeline specification
4. Continue sequentially through remaining gates
5. Update main IMPLEMENTATION_PLAN.md with completed gate details
6. Maintain consistency with specification requirements and named fixtures

## Owner

**Responsible**: System Architect + Product Owner
**Timeline**: To be completed before Gate 4 implementation begins
**Dependencies**: Finalized IMPLEMENTATION_PLAN.md with all gates detailed