# ADR-0002: No Autonomous Spend or Publication - Human Approval Required

## Status
Accepted

## Context
The Advertified system handles client budgets, creative work that goes public, supplier commitments, and external communications. These are material commercial consequences with financial and legal implications.

Several design approaches were considered:
1. **Full autonomy**: Agents could execute spend, publication, and supplier commitments independently
2. **Configurable autonomy**: Different levels of autonomy per client or account type
3. **Human approval required**: Every material consequence requires named human approval

## Decision
**Advertified will require human approval for all material commercial consequences.**

### Explicit No-Autonomy Policy

The following actions ALWAYS require named human approval:
- Client budget spend (any amount)
- Creative publication to public channels
- Supplier commitment (bookings, contracts)
- External communication (proposals, emails, outreach)
- Material commercial changes (rate changes, plan modifications)
- Payment initiation

### Rationale
1. **Financial accountability**: Client budgets must be controlled by authorized representatives
2. **Legal protection**: Creative publication requires brand and legal compliance checks
3. **Commercial risk**: Supplier commitments have contractual and financial implications
4. **Brand protection**: External communications represent the brand and require oversight
5. **Client trust**: Clients expect control over their spend and public presence
6. **Liability management**: Human approval provides clear accountability and audit trail

### Alternatives Considered
- **Full autonomy**: Rejected due to unacceptable financial and legal risk
- **Configurable autonomy**: Rejected due to complexity and potential for misconfiguration
- **Hybrid approach**: Rejected due to unclear boundaries and potential for accidental autonomy

## Consequences

### Positive
- Clear accountability for all commercial decisions
- Reduced financial and legal risk
- Client confidence in platform control
- Audit trail with named decision-makers
- Alignment with client expectations

### Negative
- Slower execution speed for routine actions
- Higher operational overhead for approval management
- Potential bottleneck if approvers are unavailable
- More complex workflow design

### Mitigations
- Fast-track approval processes for low-risk, high-volume actions
- Parallel approval workflows where appropriate
- Clear SLAs for approval response times
- Automated routing and notification systems
- Delegation mechanisms for approver unavailability

## Implementation
1. **Commercial API enforces approval gates**: No spend/publication/commitment commands execute without explicit approval record
2. **Workflow design**: Every material consequence workflow includes explicit human approval step
3. **UI design**: Clear indication of approval requirements and current approver
4. **Audit trail**: Every approval records approver, timestamp, decision, and rationale
5. **Agent contracts**: Agents explicitly forbidden from executing external consequences
6. **Tool policy**: External consequence tools require human approval parameter

## Scope and Boundaries

### In Scope (Requires Approval)
- All client budget spend
- All creative publication
- All supplier commitments
- All external communications
- Material commercial changes
- Payment initiation

### Out of Scope (May Be Automated)
- Internal calculations and computations
- Data processing and validation
- Notification generation (within approved parameters)
- Draft generation (not final delivery)
- Analysis and recommendations
- Routine data updates within approved parameters

## Configuration and Exceptions

**No exceptions to core policy**: The no-autonomy policy is non-negotiable for material consequences.

**Configuration options**:
- Approval routing based on amount thresholds
- Delegation chains for approver unavailability
- Fast-track processes for low-risk actions
- Bulk approval for similar low-risk items

**Exception process**: Any exception to this policy requires explicit ADR with legal and commercial sign-off.

## References
- Advertified Unified Specification v1.1, Section 1 (Executive decisions)
- Advertified Unified Specification v1.1, Section 6 (Agent operating model)
- Agent contract specifications (Section 22)

## Participants
- Product Owner - Business requirements
- Commercial Lead - Financial processes
- Legal Counsel - Legal compliance
- Engineering Lead - Technical implementation

## Date
2026-08-29