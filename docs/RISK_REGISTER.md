# Advertified Unified Risk Register

## Risk Assessment Framework

**Scoring system**:
- **Probability**: 1 (Rare) to 5 (Almost Certain)
- **Impact**: 1 (Negligible) to 5 (Catastrophic)
- **Risk Score**: Probability × Impact (1-25)
- **Risk Level**: Low (1-4), Medium (5-9), High (10-15), Critical (16-25)

## Current Risks

### 1. AI Provider Integration Complexity

**Risk ID**: R001
**Category**: Technical
**Description**: AWS Bedrock AgentCore integration may be more complex than anticipated, requiring additional development time and expertise.

**Probability**: 3 (Moderate)
**Impact**: 4 (Major)
**Risk Score**: 12 (High)

**Affected Gates**: 4-13 (all agent-dependent gates)

**Mitigation Strategy**:
- Use deterministic provider for all testing before live integration
- Phase rollout: Start with one agent, expand gradually
- Engage AWS support early for architectural guidance
- Allocate buffer time in Gate 4 for provider integration

**Contingency Plan**:
- Fallback to alternative orchestration if AgentCore proves unsuitable
- Reduced feature set if provider integration timeline extends significantly

**Owner**: AI Engineer
**Review Date**: 2026-09-15
**Status**: Active

---

### 2. Inventory Extraction for Unseen Files

**Risk ID**: R002
**Category**: Technical
**Description**: Document extraction pipeline may fail on unseen supplier file formats, reducing inventory coverage and requiring manual intervention.

**Probability**: 4 (Likely)
**Impact**: 4 (Major)
**Risk Score**: 16 (Critical)

**Affected Gates**: 6 (Inventory truth)

**Mitigation Strategy**:
- Extensive labelled corpus covering major document classes
- Gradual channel support expansion (OOH/Radio first)
- Human review workflow for extraction failures
- Supplier communication for format standardisation

**Contingency Plan**:
- Manual fallback process for problematic suppliers
- Reduced initial channel coverage if extraction proves unreliable
- Extended timeline for full channel support

**Owner**: Inventory Operations Lead
**Review Date**: 2026-09-30
**Status**: Active

---

### 3. Multi-Step Approval Workflow Impact on Business Velocity

**Risk ID**: R003
**Category**: Business Process
**Description**: Four-step approval process (business_interpretation → strategy → opportunity_intelligence → brief_drafting) may slow new-business velocity compared to competitors.

**Probability**: 3 (Moderate)
**Impact**: 3 (Moderate)
**Risk Score**: 9 (Medium)

**Affected Gates**: 4-5 (Evidence and opportunity, Brief)

**Mitigation Strategy**:
- Parallel processing where approval gates allow
- Fast-track process for low-risk, high-velocity opportunities
- Clear SLAs for each approval step
- Automated routing and notification

**Contingency Plan**:
- Configurable approval steps per account type
- Emergency override process for time-sensitive opportunities
- Process optimisation based on actual usage data

**Owner**: Product Owner
**Review Date**: 2026-10-15
**Status**: Active

---

### 4. Large Catalogue Performance Degradation

**Risk ID**: R004
**Category**: Technical
**Description**: Catalogue performance may degrade as inventory grows beyond 10,000 products, affecting user experience and supplier operations.

**Probability**: 3 (Moderate)
**Impact**: 4 (Major)
**Risk Score**: 12 (High)

**Affected Gates**: 6 (Inventory truth), 10 (Supplier marketplace)

**Mitigation Strategy**:
- Server-side pagination and virtualization from day one
- Comprehensive indexing strategy (tenant, channel, geography, status)
- Performance testing with synthetic large datasets
- Caching strategy for frequently accessed data

**Contingency Plan**:
- Database scaling (read replicas, partitioning)
- Search service integration (Elasticsearch) if PostgreSQL proves insufficient
- Reduced catalogue features if performance cannot meet SLAs

**Owner**: Database Engineer
**Review Date**: 2026-10-01
**Status**: Active

---

### 5. POPIA Compliance Delays

**Risk ID**: R005
**Category**: Legal/Compliance
**Description**: POPIA compliance requirements may delay production launch, particularly around data subject rights and cross-border transfer restrictions.

**Probability**: 3 (Moderate)
**Impact**: 5 (Catastrophic)
**Risk Score**: 15 (High)

**Affected Gates**: 12 (Hardening), 13 (Production launch)

**Mitigation Strategy**:
- Engage POPIA legal counsel early (Gate 2)
- Design compliance requirements from day one
- AWS af-south-1 region deployment (no cross-border transfer)
- Comprehensive processing register and privacy impact assessment

**Contingency Plan**:
- Staged launch with limited data collection if full compliance delayed
- Extended timeline for full feature set if compliance requires additional work
- Legal review buffer in Gate 12 timeline

**Owner**: Privacy Lead + Legal Counsel
**Review Date**: 2026-09-01
**Status**: Active

---

### 6. Cost Overruns from AI Usage

**Risk ID**: R006
**Category**: Financial
**Description**: AI model usage costs may exceed budgeted amounts, particularly during testing and initial deployment phases.

**Probability**: 3 (Moderate)
**Impact**: 3 (Moderate)
**Risk Score**: 9 (Medium)

**Affected Gates**: 4-13 (all agent-dependent gates)

**Mitigation Strategy**:
- Per-run budget caps enforced at runtime
- Per-tenant cost limits with alerts
- Zero live/paid Bedrock during certification
- Detailed cost tracking and reporting per workflow

**Contingency Plan**:
- Fallback to deterministic provider if costs exceed thresholds
- Reduced agent usage or simplified prompts
- Additional budget allocation if business value justifies

**Owner**: Engineering Lead + Finance
**Review Date**: 2026-10-15
**Status**: Active

---

### 7. Talent Availability for Specialized Roles

**Risk ID**: R007
**Category**: Resource
**Description**: Difficulty hiring or retaining specialized talent (AI engineers, PostgreSQL/PostGIS experts, POPIA specialists) may delay timeline.

**Probability**: 4 (Likely)
**Impact**: 4 (Major)
**Risk Score**: 16 (Critical)

**Affected Gates**: All gates

**Mitigation Strategy**:
- Early hiring for critical specialized roles
- Contractor/consultant engagement for gaps
- Training programs for existing team members
- Vendor partnerships for specialized components

**Contingency Plan**:
- Reduced feature set if critical talent unavailable
- Extended timeline with reduced team capacity
- Outsource specific components if internal capacity insufficient

**Owner**: Engineering Manager + HR
**Review Date**: 2026-09-15
**Status**: Active

---

### 8. Integration Complexity with External Systems

**Risk ID**: R008
**Category**: Technical
**Description**: Integrations with maps providers, payment systems, email services, and measurement platforms may prove more complex than anticipated.

**Probability**: 3 (Moderate)
**Impact**: 3 (Moderate)
**Risk Score**: 9 (Medium)

**Affected Gates**: 7-11 (Planning through Campaign delivery)

**Mitigation Strategy**:
- Provider-neutral interfaces from day one
- Contract testing with deterministic fakes
- Gradual integration rollout per provider
- Comprehensive error handling and fallback strategies

**Contingency Plan**:
- Manual workarounds for failed integrations
- Alternative provider selection if integration proves problematic
- Reduced feature set if critical integrations delayed

**Owner**: Integration Engineer
**Review Date**: 2026-11-01
**Status**: Active

---

### 9. User Adoption Resistance to Multi-Step Workflows

**Risk ID**: R009
**Category**: Business Process
**Description**: Users may resist the structured multi-step workflows, particularly if they're accustomed to more flexible or faster processes.

**Probability**: 3 (Moderate)
**Impact**: 3 (Moderate)
**Risk Score**: 9 (Medium)

**Affected Gates**: 3-11 (Authenticated shell through Campaign delivery)

**Mitigation Strategy**:
- User involvement in UX design from early gates
- Clear communication of benefits (evidence-backed decisions, reduced risk)
- Training and onboarding programs
- Configuration options for workflow flexibility where appropriate

**Contingency Plan**:
- Workflow simplification based on user feedback
- Fast-track options for low-risk scenarios
- Extended onboarding support period

**Owner**: Product Owner + UX Lead
**Review Date**: 2026-12-01
**Status**: Active

---

### 10. Data Migration from Legacy Systems

**Risk ID**: R010
**Category**: Technical
**Description**: While this is a clean build, there may be pressure to migrate historical data from legacy systems, introducing complexity and risk.

**Probability**: 2 (Unlikely)
**Impact**: 4 (Major)
**Risk Score**: 8 (Medium)

**Affected Gates**: 2 (Canonical foundation), 12 (Hardening)

**Mitigation Strategy**:
- Maintain clean build stance (legacy is read-only reference only)
- Document migration decision process with ADR
- If migration required, treat as separate project with own timeline
- Comprehensive data validation and cleansing

**Contingency Plan**:
- Parallel operation during transition period
- Selective migration of critical data only
- Extended timeline if migration complexity increases

**Owner**: Data Engineer
**Review Date**: 2026-11-15
**Status**: Monitor

---

## Risk Monitoring and Reporting

**Monthly risk review**: All risks reviewed monthly with updated probability, impact, and mitigation status

**Risk escalation**: Critical risks (16-25) escalated to executive leadership

**New risk identification**: Risk register reviewed at each gate completion for new risks

**Risk closure**: Risks closed when mitigated or no longer applicable

## Risk Metrics

**Current risk profile**:
- Critical risks: 2 (R002, R007)
- High risks: 2 (R001, R004)
- Medium risks: 5 (R003, R006, R008, R009, R010)
- Low risks: 0

**Trend**: Risk profile relatively stable, with concentration in technical and resource areas

**Focus areas**: Talent availability (R007) and inventory extraction (R002) require immediate attention