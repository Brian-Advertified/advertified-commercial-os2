# ADR-0003: Four-Step Opportunity-to-Brief Agent Split

## Status
Accepted

## Context
The opportunity-to-brief workflow could be implemented as a single monolithic "prospecting" agent or split into multiple specialized agents. The specification calls for four agents: business_interpretation → strategy → opportunity_intelligence → brief_drafting, each with its own human gate.

### Design Options Considered
1. **Single monolithic agent**: One agent handles interpretation, strategy, opportunity angles, and brief drafting
2. **Two-agent split**: Strategy/opportunity combined, brief drafting separate
3. **Three-agent split**: Business interpretation separate, strategy/opportunity combined, brief drafting separate
4. **Four-agent split** (specification): Each step as separate agent with human gate

## Decision
**Advertified will use the four-agent split as specified: business_interpretation → strategy → opportunity_intelligence → brief_drafting**

### Rationale
1. **Single responsibility**: Each agent has one clear business question to answer
2. **Testability**: Each agent can be tested independently with specific evaluation corpus
3. **Human control**: Each major decision point has explicit human approval
4. **Quality control**: Critic agent can evaluate each stage independently
5. **Explainability**: Each stage produces clear, auditable artefacts
6. **Flexibility**: Individual agents can be improved without affecting entire workflow
7. **Risk mitigation**: Errors in one stage don't propagate without human checkpoint

### Business Velocity Consideration
**Acknowledged concern**: Four approval steps may slow new-business velocity compared to competitors.

**Mitigation strategies**:
- **Parallel processing**: Strategy and opportunity intelligence can run in parallel after business interpretation
- **Fast-track process**: Low-risk, high-velocity opportunities can have accelerated approval
- **Bulk approval**: Similar opportunity types can have streamlined approval
- **SLA targets**: Each approval step has clear service level agreement
- **Delegation**: Approver delegation chains for unavailability

**Business trade-off accepted**: The quality, risk control, and explainability benefits outweigh the velocity cost for the initial release. Velocity optimization can be addressed in future iterations based on actual usage data.

## Agent Responsibilities

### 1. Business Interpretation Agent
**Question**: What does this business sell, to whom, and in what buying context?
**Input**: Approved website/file evidence
**Output**: BusinessInterpretation (business model, customer groups, occasions, geography, unknowns, hypotheses)
**Human gate**: Confirm material interpretation
**Value add**: Ensures we understand the business before generating strategy

### 2. Strategy Agent
**Question**: What growth and communications strategy follows from the evidence?
**Input**: Approved evidence, interpretation, selected opportunity angle
**Output**: StrategyVersion (diagnosis, growth thesis, objectives, audiences, proposition, message, channel implications, risks)
**Human gate**: Strategy approval
**Value add**: Provides strategic direction before tactical planning

### 3. Opportunity Intelligence Agent
**Question**: What credible advertising opportunity exists?
**Input**: Approved EvidenceSet + reviewed business interpretation
**Output**: OpportunityAngleSet (ranked opportunity angles linked to approved evidence)
**Human gate**: Human selects or rejects angle
**Value add**: Identifies specific opportunity angles within strategic context

### 4. Brief Drafting Agent
**Question**: How does approved evidence become a complete campaign brief?
**Input**: Approved StrategyVersion, opportunity, and evidence
**Output**: BriefVersion (complete brief preserving unknowns, assumptions, lineage)
**Human gate**: Brief approval
**Value add**: Translates strategy into actionable campaign brief

## Workflow Integration

### Sequential Dependencies
- Business interpretation → Strategy (interpretation required for strategy)
- Strategy → Opportunity intelligence (strategy provides context for angles)
- Business interpretation + Strategy + Opportunity intelligence → Brief drafting (all inputs required)

### Parallel Opportunities
- Opportunity intelligence can sometimes run in parallel with strategy if business interpretation is complete
- Critic agent can evaluate multiple stages independently

### Human Gate Timeline
**Target SLAs** (to be defined in Gate 4):
- Business interpretation confirmation: 4 hours
- Strategy approval: 8 hours
- Opportunity angle selection: 2 hours
- Brief approval: 8 hours

**Total target opportunity-to-brief time**: 22 hours (under 1 business day with parallel processing)

## Implementation Considerations

### Agent Coordination
- Workflow orchestrator manages agent sequence and parallel execution
- Each agent has clear input/output contracts
- Checkpoint after each human gate for resume capability
- Correlation tracking across all agents in workflow

### Human Experience
- Clear dashboard showing current stage and next action
- Parallel approval notifications where applicable
- Bulk approval for similar opportunity types
- Delegation mechanism for approver unavailability

### Performance Monitoring
- Track time-to-approval for each stage
- Identify bottlenecks in approval process
- Optimize based on actual usage data
- SLA compliance reporting

## Evolution Path

**Future optimizations** (post-initial release):
- Machine learning models to predict approval likelihood
- Automated approval for low-risk, high-confidence cases
- Reduced human gates for well-established patterns
- Parallel agent execution where dependencies allow

**Current commitment**: Four-agent split with human gates for initial release. Optimizations based on actual usage data and business feedback.

## References
- Advertified Unified Specification v1.1, Section 6 (Agent operating model)
- Advertified Unified Specification v1.1, Section 7.1 (Unbriefed opportunity to approved brief)
- Agent contract specifications (Section 22)

## Participants
- Product Owner - Business process design
- AI Engineer - Agent implementation
- Commercial Lead - New-business workflow
- UX Lead - Human experience design

## Date
2026-08-29