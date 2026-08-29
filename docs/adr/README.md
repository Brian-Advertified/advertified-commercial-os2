# Architecture decision process

ADRs record material decisions; they do not create approval by themselves.

## Required states

- **Proposed:** analysis may proceed inside the authorised gate, but the decision is not approved.
- **Accepted:** an actual accountable owner and required reviewers approved the exact ADR version on an ISO date.
- **Rejected:** the named owner declined the proposal and recorded why.
- **Superseded:** a later accepted ADR or higher authority replaced it.

An AI, role label, passing test, or document author cannot mark an ADR Accepted. `UNASSIGNED`, `TBD`, or a missing date is incompatible with Accepted status.

## ADR required before change

Create a short ADR before adding or materially changing:

- an infrastructure platform or cross-boundary dependency;
- an external provider or provider SDK;
- the closed agent roster;
- the canonical database, API ownership, tenancy, authentication, or session model;
- a consequential commercial rule;
- a security, privacy, migration, deployment, or recovery exception.

The current gate and `AGENTS.md` still control mutation authority. An accepted ADR cannot silently authorise a later gate.

## Review sequence

1. State evidence, assumptions, unknowns, scope, and decision owner.
2. Compare viable options, including security, tenant, cost, data, operations, and reversibility.
3. Record one bounded proposed decision and explicit non-decisions.
4. Run the named verification.
5. Collect actual reviewer names and dates.
6. Change status only after the accountable owner decides.
7. Link implementation and retained evidence without editing historical approval records.
