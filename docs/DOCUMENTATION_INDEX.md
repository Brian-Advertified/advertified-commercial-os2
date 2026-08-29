# Documentation authority index

Read documents according to their role. A lower row cannot override a higher row.

| Priority | Source | Purpose |
|---:|---|---|
| 1 | Repository owner's latest explicit instruction | Current human authority |
| 2 | `AGENTS.md` | Mutation, evidence, safety, and engineering rules |
| 3 | `docs/spec/README.md` and its seven parts | Complete normative v1.1 product/build specification |
| 4 | Accepted ADRs | Approved decision within their stated scope |
| 5 | Executable schemas, migrations, contracts, and tests | Machine-verifiable behavior |
| 6 | `docs/IMPLEMENTATION_PLAN.md` | Ordered gate execution index |
| 7 | Capability/status/evidence documents | Observed implementation state |
| 8 | Draft domain, permission, privacy, risk, and timeline documents | Design input only |

## Status-sensitive documents

- `docs/adr/README.md`: binding ADR ownership, status, and acceptance process.
- `docs/adr/`: only an ADR marked Accepted by an actual named owner is controlling.
- `docs/evidence/manifest.schema.json`: machine-readable gate-evidence contract.
- `docs/evidence/GATE_REPORT_TEMPLATE.md`: human-readable gate handoff template.
- `docs/evidence/gate-1/`: current Gate 1 evidence; it records PENDING and cannot self-approve the gate.
- `docs/DOMAIN_MODEL_COMPLETE.md`: legacy draft name; not an implemented or approved complete model.
- `docs/PERMISSION_MATRIX.md`: design draft; no enforcement exists yet.
- `docs/POPIA_COMPLIANCE.md`: engineering checklist; not legal advice or approval.
- `docs/GATE0_NO_GO_VERDICT.md`: historical review, superseded after remediation.
- `docs/REWRITE_REQUIRED_ACKNOWLEDGMENT.md`: historical gap closure record.
- `docs/GATES_4_13_PLACEHOLDER.md`: replaced by the normative spec and current plan.

When sources conflict, stop the affected work, record the conflict, and ask the named owner. Never select the most convenient statement.
