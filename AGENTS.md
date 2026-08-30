# Advertified repository instructions

These instructions bind every human or AI contributor working in this repository.

## 1. Authority and evidence

Use this source order when requirements conflict:

1. The repository owner's latest explicit instruction.
2. This `AGENTS.md`.
3. The normative v1.1 specification indexed at `docs/spec/README.md`.
4. A genuinely approved ADR.
5. Executable contracts, migrations, and tests.
6. Plans, status reports, and commentary.

Do not silently choose between conflicting sources. Record the conflict, keep the affected capability blocked, and ask its named owner. A missing fact remains unknown. A failed check remains failed. A scaffold is not an implementation. An implementation is not verified until repeatable evidence exists.

The clean parent history is the rollback reference. Inspect `git status` and the relevant files before editing. Preserve unrelated user work. Never copy code, schema, migrations, secrets, or configuration from older Advertified repositories.

## 2. Current permission boundary

Local non-production gates may proceed sequentially under Brian Rabuthu's standing direction of
2026-08-29 without pausing for repetitive gate approvals. Before implementation, record an
exact work packet and verify the preceding gate; then deliver and retain repeatable evidence.
Do not skip gate order or report a gate delivered without its acceptance evidence.

Never commit, push, merge, deploy, mutate cloud resources, use production data, or contact an external party unless the repository owner explicitly requests that exact action. Local non-production builds, tests, and Docker operations are permitted when required by an authorised task.

Live or paid AI/provider calls are forbidden during redevelopment. `AWS_BEDROCK_ENABLED` and equivalent provider switches default to false. Tests must use deterministic local fixtures or fakes. Never broaden a permission, budget, audience, tenant scope, or commercial action to get past a blocker.

## 3. Locked technology and ownership boundaries

- Web: React 19.2.0, TypeScript, and Vite.
- Canonical commercial API: C# 14 on .NET 10.
- Agent runtime: Python 3.12-compatible FastAPI.
- Database: PostgreSQL 16 with PostGIS and pgvector.
- Local object storage: an S3-compatible service.
- Cache and queues: Redis where an approved design requires it.

The C# Commercial API owns canonical business state, authorisation, tenant enforcement, lifecycle transitions, approvals, idempotency, audit, and commercial commands. Python agents may propose typed outputs only through authorised API contracts. Python must not connect directly to PostgreSQL, issue SQL, mutate commercial state, approve its own work, spend money, publish, book, invoice, or communicate externally. React must not contain persistence code, server secrets, or direct provider credentials.

Opportunity discovery and a supplied client Brief are distinct entry paths. A supplied Brief remains the source artefact and is never fabricated through an Opportunity workflow. The canonical lifecycle is Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning.

`OOH_ONLY` and `FULL_CAMPAIGN` are immutable campaign-mode selections on that one canonical lifecycle. `OOH_ONLY` only restricts selectable channels to OOH/DOOH; `FULL_CAMPAIGN` permits the full channel registry. Never create a separate Rapid OOH aggregate, namespace, endpoint family, migration, permission set, route decision, proposal guard, or restart/expansion workflow. A changed OOH-only requirement starts a new CampaignBrief from the beginning.

## 4. Engineering guardrails

Apply SOLID, explicit dependencies, cohesive modules, and one canonical owner for every rule. Prefer simple code over speculative abstraction. Do not create parallel domain models or temporary alternate sources of truth.

For authored source files:

- Hard limit: 400 physical lines per file.
- Target: 300 lines or fewer.
- Target: functions under 40 nonblank, noncomment lines.
- Hard function limit: 60 nonblank, noncomment lines unless an approved ADR explains why.
- Cyclomatic complexity target: 10 or lower.
- No god classes, god hooks, god services, or miscellaneous utility dumping grounds.
- Split by business responsibility, not arbitrary numbered fragments.

Generated files and the split normative specification are exempt only where the generator or document index says so. Do not hand-edit generated output.

No magic domain strings or unexplained numbers in application logic. Lifecycle states, roles, permissions, channels, rejection reasons, proposal tiers, rate types, currencies, feature keys, integration types, and other governed vocabularies belong in versioned master/reference data with:

- stable, never-repurposed code;
- human display label;
- active flag;
- deterministic sort order;
- effective dates where applicable;
- metadata schema;
- migration and audit history;
- validation that definitions and seed records agree.

Constants are acceptable only for technical protocol values local to one boundary. User-facing wording belongs in content resources. Secrets belong in secret stores or ignored local environment files, never source control.

## 5. Commercial and AI safety

No autonomous spend, publication, supplier commitment, booking, payment, invoice, external communication, or material commercial change. Each requires the correct named human approval, exact immutable artefact version, tenant context, and audit event.

AI output is untrusted proposal data. Validate it against typed schemas, evidence citations, authorised tools, budgets, tenant scope, and lifecycle rules. Persist input versions, prompt/config version, model/provider identity, tool calls, cost estimate, validation outcome, and human disposition when those capabilities are implemented.

Never invent inventory, rates, availability, audience data, evidence, supplier responses, approvals, client decisions, legal conclusions, performance, or completion evidence. Use `unknown`, `not supplied`, or a blocked state. “Evidence, not confidence” is the operating rule.

If an instruction is ambiguous, high-impact, irreversible, externally visible, or requires unavailable authority, stop that action and report:

1. the exact blocker;
2. the smallest owner decision needed;
3. safe work that can continue;
4. the verification needed after the decision.

## 6. Required workflow for every change

Before editing:

1. Read this file and the nearest nested `AGENTS.md`, if any.
2. Read the applicable specification sections and ADRs.
3. Inspect status, current implementation, contracts, tests, migrations, and recent evidence.
4. State the bounded requirement and acceptance evidence.
5. Confirm the work belongs to the currently authorised gate.

During editing:

1. Make the smallest coherent vertical change.
2. Preserve tenant and permission boundaries.
3. Add only the smallest test that proves an acceptance rule, domain invariant, security boundary, migration behavior, or real regression.
4. Consolidate equivalent cases with parameterised data. Do not duplicate suites, test framework/library behavior, chase coverage percentages, or inflate test counts.
5. Use deterministic fixtures.
6. Keep documentation and capability status truthful.
7. Do not weaken a guardrail to make a check pass.

After editing:

1. Inspect the complete diff.
2. Run targeted tests and all affected builds.
3. Run architecture tests.
4. Validate Compose for infrastructure changes.
5. Run an affected user journey when that journey exists.
6. Update evidence with exact commands and outcomes.
7. Leave the tree unstaged unless the owner explicitly asks for a commit.

A completion report must distinguish created, implemented, tested, verified, and blocked. It must list exact failing checks. Percent-complete claims without a defined denominator are forbidden.

## 7. Definition of done

A task is done only when:

- acceptance criteria are explicit;
- code, tests, and docs agree;
- affected builds and tests pass;
- boundaries and line limits pass;
- no live provider or production resource was used;
- security and tenant implications were checked;
- migrations are forward-safe and tested when data changes;
- the diff contains no unrelated changes or secrets;
- retained evidence is sufficient for another developer to reproduce the result.

No AI agent may declare a gate, security review, legal review, production readiness, or its own correctness approved. Those decisions belong to the named human owner and independent evidence.
