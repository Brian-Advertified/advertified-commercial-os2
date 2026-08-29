# Timeline and capacity decision

**Status:** BLOCKED — INPUTS AND NAMED OWNER UNASSIGNED  
**Prior 28–38 week range:** WITHDRAWN; do not quote or plan against it

No schedule can be generated from gate count alone.

## Inputs required

| Input | Required detail |
|---|---|
| People | Actual names, roles, skill constraints, start dates |
| Capacity | Available days/FTE per person after other commitments |
| Work packages | Gate packet deliverables and bottom-up ranges |
| Dependencies | Sequential, parallel, external, and critical path |
| External lead time | Identity, maps, email, payment, suppliers, AWS/provider, legal/privacy/security |
| Data/evaluation | Corpus creation, labelling, review, supplier catalogue work |
| Quality | Test automation, UAT, accessibility, security assessment |
| Operations | Environments, migration rehearsal, recovery drills, runbooks |
| Uncertainty | Best/expected/worst ranges and explicit assumptions |
| Stabilisation | Post-launch support and defect/capacity reserve |

## Estimation method

1. Complete and approve the next gate packet.
2. Decompose it into independently testable work packages.
3. Estimate by the people who will perform/review the work.
4. Record optimistic, expected, and pessimistic effort with assumptions.
5. Map dependencies and external decision lead times.
6. Load against real capacity, not headcount.
7. include review, rework, integration, UAT, recovery, and stabilisation.
8. Reforecast after each gate using observed throughput and defects.

AI may assist arithmetic or scenario comparison but may not invent people, capacity, vendor dates, approvals, or commitment. Until these inputs exist, only gate order—not calendar duration—is authoritative.
