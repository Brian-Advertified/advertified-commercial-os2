# Advertified Unified - Implementation Plan

## Overview

This document outlines the systematic implementation of the Advertified Unified system according to the Production Build Specification v1.1. The implementation follows 13 ordered gates, each building upon the previous to ensure a coherent, tested system.

## Implementation Philosophy

- **Vertical Integration**: Each gate implements end-to-end user-visible value
- **Test-First**: Tests define the acceptance criteria before implementation
- **No Mocks**: Use real dependencies where possible, deterministic fakes where necessary
- **Incremental**: Small, verifiable changes that can be rolled back
- **Evidence-Based**: Completion is proven by tests, not code inspection

## Gate 0: Repository Baseline ✅

**Status**: COMPLETED

**Objectives**:
- Create foundational project structure
- Document setup procedures
- Establish capability tracking
- Define repository boundaries

**Completed**:
- ✅ Created directory structure (web, api, agent-runtime, workers, shared, infrastructure, tests, docs)
- ✅ Set up Docker Compose for local development
- ✅ Created environment configuration template
- ✅ Initialized capability ledger
- ✅ Created ADR template
- ✅ Set up .NET Web API project
- ✅ Set up React/Vite project
- ✅ Set up Python/FastAPI agent runtime
- ✅ Created common schemas
- ✅ Created setup guide

**Evidence**:
- Project structure exists
- README.md with overview
- CAPABILITY_LEDGER.md tracking system
- docker-compose.yml with all infrastructure services
- .env.example configuration template
- ADR template for architecture decisions
- Setup guide for developers

## Gate 1: Architecture Guardrails

**Status**: PENDING

**Objectives**:
- Implement CI rules and analyzers
- Set up master data registry
- Create architecture validation
- Establish code quality gates

**Key Tasks**:
1. Set up GitHub Actions CI pipeline
2. Implement 400-line file size checker
3. Configure code analyzers (ESLint, SonarQube, Roslyn)
4. Create master data registry schema
5. Set up dependency scanning
6. Implement secret scanning
7. Create architecture boundary tests

**Exit Evidence**:
- CI pipeline runs and fails on known violations
- Master data registry with initial seeds
- Architecture tests enforce boundaries
- Dependency scanning configured
- Secret scanning operational

## Gate 2: Canonical Foundation

**Status**: PENDING

**Objectives**:
- Implement core domain models
- Set up audit and idempotency
- Create outbox pattern
- Establish database migrations
- Generate OpenAPI contracts

**Key Tasks**:
1. Implement Tenant, User, Membership domain models
2. Create value objects (Money, VAT, etc.)
3. Implement audit event system
4. Create idempotency mechanism
5. Implement outbox pattern for events
6. Set up EF Core migrations
7. Generate OpenAPI specification
8. Create integration tests

**Exit Evidence**:
- Tenant isolation tests pass
- Audit events are recorded
- Idempotency prevents duplicate operations
- Outbox publishes events reliably
- OpenAPI spec is generated and versioned
- Migration tests pass (empty and upgrade)

## Gate 3: Authenticated Shell

**Status**: PENDING

**Objectives**:
- Implement authentication and authorization
- Create role-based dashboards
- Set up route guards
- Implement error and accessibility states

**Key Tasks**:
1. Implement OIDC authentication
2. Create role-based authorization
3. Build workspace selection interface
4. Create role-specific dashboards
5. Implement route guards
6. Create error state components
7. Implement accessibility features
8. Create Playwright tests for auth journeys

**Exit Evidence**:
- Users can sign in and select workspaces
- Role dashboards show correct KPIs
- Route guards prevent unauthorized access
- Error states are handled gracefully
- Accessibility checks pass (WCAG 2.2 AA)
- E2E-01 (Tenant isolation) passes

## Gate 4: Evidence and Opportunity

**Status**: PENDING

**Objectives**:
- Implement evidence capture and review
- Create business interpretation agent
- Implement opportunity intelligence agent
- Create strategy and critic agents

**Key Tasks**:
1. Implement file upload and evidence capture
2. Create crawl policy and worker
3. Build evidence review interface
4. Implement Business Interpretation agent
5. Implement Opportunity Intelligence agent
6. Implement Strategy agent
7. Implement Critic & Readiness agent
8. Create agent evaluation framework

**Exit Evidence**:
- Evidence can be captured and reviewed
- Business interpretation produces structured artefacts
- Opportunity angles are generated and ranked
- Strategy is created with critic objections
- E2E-02 (Unbriefed opportunity) passes

## Gate 5: Canonical Brief

**Status**: PENDING

**Objectives**:
- Implement CampaignBrief aggregate
- Create immutable BriefVersions
- Implement brief comparison
- Track unknowns and assumptions
- Create approval workflow

**Key Tasks**:
1. Implement CampaignBrief domain model
2. Create BriefVersion immutability
3. Build brief comparison interface
4. Implement unknowns tracking
5. Create brief approval workflow
6. Implement stale downstream logic
7. Create Brief Drafting agent

**Exit Evidence**:
- BriefVersions are immutable
- Brief comparison shows changes clearly
- Unknowns are tracked and visible
- Approval workflow enforces human gates
- Stale inputs invalidate downstream artefacts
- E2E-02 reaches approved BriefVersion

## Gate 6: Inventory Truth

**Status**: PENDING

**Object Objectives**:
- Implement import pipeline
- Create document classification
- Implement structure extraction
- Build review interface
- Create channel schemas
- Implement catalogue search

**Key Tasks**:
1. Implement file upload and malware scanning
2. Create document classification
3. Integrate Docling for structure extraction
4. Implement asset extraction
5. Build evidence linking
6. Create human review interface
7. Implement channel-specific schemas
8. Create catalogue search with pagination
9. Build detail pages
10. Implement supplier ownership

**Exit Evidence**:
- Unseen files can be processed without code changes
- Precision/recall meet thresholds on labelled corpus
- Large catalogue (10k+) remains performant
- Review interface supports bulk operations
- E2E-05 (Unseen inventory file) passes

## Gate 7: Planning

**Status**: PENDING

**Objectives**:
- Implement audience agent
- Create media mix agent
- Implement eligibility engine
- Build shortlist generation
- Create benchmark calculations
- Implement supply forecast
- Create media plan approval

**Key Tasks**:
1. Implement Audience agent
2. Implement Media Planning agent
3. Create eligibility engine
4. Implement shortlist generation
5. Build benchmark calculations
6. Integrate supply forecast services
7. Create media plan approval workflow
8. Implement stale input handling

**Exit Evidence**:
- Audience definitions are evidence-backed
- Media mix allocations reconcile to budget
- Eligibility engine applies hard constraints
- Shortlist includes rejection reasons
- Benchmarks are comparable across products
- E2E-03 (Full campaign) reaches approved plan

## Gate 8: Proposal

**Status**: PENDING

**Objectives**:
- Implement tier configuration
- Create proposal narrative agent
- Implement proposal critic
- Create document generation
- Build proposal sending workflow

**Key Tasks**:
1. Implement tier configuration system
2. Create Proposal Narrative agent
3. Implement proposal critic
4. Build total calculations
5. Integrate document generation (DOCX/PDF)
6. Create proposal approval workflow
7. Implement proposal sending
8. Track AI costs per proposal

**Exit Evidence**:
- Tiers are materially different
- Narratives are client-ready
- Calculations reconcile correctly
- Branded documents are generated
- Sending requires human approval
- AI costs are tracked and visible
- E2E-03/E2E-07 pass

## Gate 9: Rapid OOH

**Status**: PENDING

**Objectives**:
- Implement automatic path detection
- Create geography resolution
- Implement OOH eligibility
- Build supplier confirmation
- Create recalculation logic

**Key Tasks**:
1. Implement Rapid OOH path detection
2. Integrate maps provider for geography
3. Implement route/POI handling
4. Create OOH-specific eligibility
5. Build supplier confirmation workflow
6. Implement recalculation on responses

**Exit Evidence**:
- Brief determines OOH path automatically
- Geography/routes/POIs are resolved
- OOH eligibility applies format constraints
- Supplier confirmation is tracked
- Recalculation updates plans correctly
- E2E-04 (Rapid OOH) passes

## Gate 10: Supplier Marketplace

**Status**: PENDING

**Objectives**:
- Implement supplier user management
- Create listing management
- Build freshness tracking
- Implement RFQ system
- Create booking workflow

**Key Tasks**:
1. Implement supplier user management
2. Create listing creation interface
3. Build freshness tracking system
4. Implement RFQ system
5. Create supplier response workflow
6. Build booking management
7. Implement commercial settings

**Exit Evidence**:
- Suppliers can manage their own listings
- Freshness is tracked and alerts sent
- RFQs can be sent and responded to
- Bookings are tracked through completion
- E2E-06 (Supplier self-service) passes

## Gate 11: Campaign Delivery

**Status**: PENDING

**Objectives**:
- Implement creative workflow
- Create booking management
- Build proof submission
- Implement performance tracking
- Create measurement interpretation

**Key Tasks**:
1. Implement creative asset management
2. Create booking workflow
3. Build proof submission system
4. Integrate performance data
5. Implement Measurement agent
6. Create client reporting
7. Build campaign lifecycle

**Exit Evidence**:
- Creative assets are versioned and approved
- Bookings are tracked through delivery
- Proofs are submitted and reviewed
- Performance data is imported and interpreted
- Client reports are generated
- E2E-08 (Campaign delivery) passes

## Gate 12: Hardening

**Status**: PENDING

**Objectives**:
- Implement recovery mechanisms
- Strengthen security controls
- Ensure POPIA compliance
- Optimize performance
- Establish observability
- Create backup/restore procedures

**Key Tasks**:
1. Implement checkpoint recovery
2. Strengthen authentication/authorization
3. Implement POPIA controls
4. Optimize database queries
5. Set up monitoring and alerting
6. Create backup/restore procedures
7. Write operational runbooks
8. Perform security audit

**Exit Evidence**:
- Recovery works from checkpoints
- Security controls are enforced
- POPIA requirements are met
- Performance meets SLOs
- Monitoring detects issues
- Backups can be restored
- E2E-09/10/12 pass

## Gate 13: Production Launch

**Status**: PENDING

**Objectives**:
- Complete 30-case certification
- Achieve zero-Bedrock certification
- Obtain unanimous greenlight
- Deploy to production
- Set up production monitoring
- Complete handover

**Key Tasks**:
1. Execute 30 genuine test cases
2. Verify zero live/paid Bedrock calls
3. Obtain stakeholder sign-offs
4. Deploy to AWS af-south-1
5. Set up production monitoring
6. Configure alerts and dashboards
7. Complete operations handover
8. Document known limitations

**Exit Evidence**:
- 30 accepted cases with retained evidence
- Zero live/paid Bedrock during certification
- All stakeholders have signed off
- Production deployment is successful
- Monitoring detects production issues
- Handover documentation is complete

## Success Criteria

The system is considered complete when:

1. **All Gates Pass**: Each gate's exit evidence is verified
2. **Tests Pass**: All unit, integration, and E2E tests pass
3. **Security Passes**: Security audit finds no critical issues
4. **Performance Meets SLOs**: p95 API latency < 2s, 99.5% availability
5. **Documentation Complete**: All required documentation exists
6. **Stakeholder Approval**: All named stakeholders sign off

## Risk Management

### High-Risk Areas

1. **AI Provider Integration**: Bedrock integration complexity
   - **Mitigation**: Use deterministic provider for testing, gradual rollout

2. **Inventory Extraction**: Unseen file processing
   - **Mitigation**: Extensive labelled corpus, gradual channel support

3. **Data Migration**: Legacy system transition
   - **Mitigation**: Clean build approach, legacy as read-only reference

4. **Performance**: Large catalogue operations
   - **Mitigation**: Server-side pagination, indexing, virtualization

### Rollback Strategy

Each gate has a defined rollback plan:
- Database migrations can be reversed
- Feature flags can disable new functionality
- Previous Docker images can be redeployed
- Data changes are audited and reversible

## Timeline Estimate

- **Gate 0-3**: Foundation (4-6 weeks)
- **Gate 4-5**: Core workflow (6-8 weeks)
- **Gate 6-8**: Planning and proposals (8-10 weeks)
- **Gate 9-11**: Marketplace and delivery (6-8 weeks)
- **Gate 12-13**: Hardening and launch (4-6 weeks)

**Total Estimate**: 28-38 weeks

## Dependencies

### External Dependencies
- AWS Bedrock access and credentials
- OIDC provider configuration
- Maps provider API keys
- Payment provider setup
- Email provider (Resend) configuration

### Internal Dependencies
- Each gate depends on previous gates
- Agent runtime depends on Commercial API
- Web application depends on API contracts
- Workers depend on both API and runtime

## Notes

- This plan follows the specification exactly
- No gates are skipped or reordered
- Each gate produces working, tested software
- Evidence-based completion, not assumptions
- Stakeholder approval at critical points