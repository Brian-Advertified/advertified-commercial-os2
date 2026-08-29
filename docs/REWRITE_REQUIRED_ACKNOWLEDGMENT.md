# Implementation Plan Rewrite Required - Complete Scope

**Status**: CRITICAL REWRITE NEEDED - Current plan is not sign-off-able

## Issues Addressed So Far

✅ **Repository verification**: Added actual command output for git status, commit info
✅ **Stack version deviation**: Created ADR-0004 for React 19.2.8 vs 19.2.0 conflict
✅ **Proposal tiers correction**: Fixed to use Advertified's actual bands (Launch, Boost, Scale, Dominance)
✅ **Domain model expansion**: Added complete domain model with all 25+ aggregates
✅ **Ownership clarification**: Documented that current "owners" are fictional roles, not people

## Critical Issues Still Requiring Complete Rewrite

### 1. Agent Registry (Complete Rewrite Required)
**Current state**: Generic template only
**Required**: Actual contracts for all 11 agents with:
- Schema identifiers and versions
- Specific tools (allowed and forbidden)
- Input/output aggregate versions
- Evidence policy specifics
- Human gate definitions
- Cost caps per agent
- Timeout and retry rules
- Checkpoint behavior
- Evaluation corpus for each agent
- Required adversarial cases
- Failure states and recovery

### 2. Tool Registry (Complete Rewrite Required)
**Current state**: Not defined
**Required**: Complete tool registry for:
- Inventory search tool
- Product-purpose interpretation tool
- Inventory eligibility tool
- Inventory shortlisting tool
- Commercial benchmarking tool
- Rapid OOH requirements tool
- Geography/route/POI resolution tool
- Supplier confirmation tool
- Shortlist recalculation tool
- Each tool: schema, permissions, evidence policy, failure handling

### 3. Screen Register (Complete Rewrite Required)
**Current state**: Route list with generic template
**Required**: Complete contracts for 30+ screens with:
- Exact user outcome in commercial language
- Primary action definition
- Business-language heading and instructions
- Data contract (specific fields, sources)
- Role permissions matrix
- All states: empty/loading/error/forbidden/stale
- Validation behavior specifics
- Recovery action definition
- Mobile and desktop behavior
- Accessibility test requirements
- Playwright fixture definition
- Reference viewport screenshot requirement

### 4. Gates 4-13 (Complete Rewrite Required)
**Current state**: Stubbed with "[Continuing with remaining gates...]"
**Required**: Complete detailed specifications for:
- Gate 4: Evidence and Opportunity (complete agent specs)
- Gate 5: Canonical Brief (aggregate specifics, approval workflows)
- Gate 6: Inventory Truth (complete ingestion pipeline)
- Gate 7: Planning (audience, media mix, commercial truth)
- Gate 8: Proposal (financial calculations, document generation)
- Gate 9: Rapid OOH (corrected dependencies, scenario binding)
- Gate 10: Supplier Marketplace (ownership rules, RFQ system)
- Gate 11: Campaign Delivery (measurement specifics, lifecycle)
- Gate 12: Hardening (certification focus, not introduction)
- Gate 13: Production Launch (30-case certification details)

### 5. Dependency Corrections (Complete Rewrite Required)
**Current state**: Incorrect dependencies (Rapid OOH depends on Proposal)
**Required correction**: Rapid OOH is specialized planning path:
- Brief → OOH-path decision → geography/routes/POIs → verified inventory → deterministic eligibility → shortlist → supplier confirmation → recalculation → human selection → approved plan → proposal
- Proposal generation is downstream of approved Rapid OOH plan
- Shared proposal infrastructure can be built earlier, but dependency must reflect correct sequence

### 6. Inventory Pipeline (Complete Rewrite Required)
**Current state**: Simplified pipeline missing critical steps
**Required complete pipeline**:
- Upload → quarantine → malware/type validation → classify → immutable source preservation → render → structure extraction → coordinate/table reconstruction → asset extraction → evidence linking → candidate normalisation → validation → duplicate/supersession resolution → human review → approved publication → precision/recall evaluation
- Must include DOCX and XLS explicitly
- Must define precision/recall thresholds
- Must specify partial publication and correction processes

### 7. Rollback Strategy (Complete Rewrite Required)
**Current state**: Unsafe "migrations must pass rollback"
**Required**: Safe rollback strategy:
- Empty-database forward migration
- Representative upgrade migration
- Expand-migrate-contract pattern
- Backward-compatibility verification
- Compensating migration or command
- Restore test verification
- Rollback classification (reversible vs compensating)
- Point-in-time recovery procedures
- "No reverse migration" acceptable when explicitly approved with tested restoration

### 8. Security Architecture (Complete Rewrite Required)
**Current state**: "secure HttpOnly cookie or bearer token" - not a decision
**Required**: Explicit security architecture:
- Exact browser authentication design choice
- CSRF model specification
- Token storage rules (HttpOnly cookie vs local storage vs session storage)
- Refresh behavior definition
- Logout/revocation behavior
- Service-to-service authentication model
- OIDC provider choice or explicit "owner-controlled blocker" with provider-neutral adapter requirement

### 9. Master Data Classification (Complete Rewrite Required)
**Current state**: Combined lifecycle list is unsafe
**Required**: Proper classification:
- Technical protocol state → Typed code enum
- Commercial lifecycle configuration → Versioned database master data
- Role and permission definitions → Stable seeded registry
- Channels and product types → Versioned master data
- Command and event identifiers → Typed contracts
- Human-facing labels → Localised database/configuration values
- Proposal bands and fee policies → Account/master-data configuration
- Each aggregate needs its own state machine and permitted transitions

### 10. Brief Aggregate Correction (Complete Rewrite Required)
**Current state**: Audiences listed as required could force user to provide complete audience definitions
**Required**: Proper distinction:
- Source-supplied facts (user provides)
- System interpretation (AI generates)
- Approved evidence (human approves)
- Client confirmations (user confirms)
- Reasoned hypotheses (AI generates, labelled)
- Planning assumptions (system tracks)
- Unknowns (system acknowledges)
- Downstream audience research (separate stage)
- Brief may contain preliminary audience direction without pretending completed audience research exists

### 11. Ownership Assignment (Complete Rewrite Required)
**Current state**: Fictional role labels (System Architect, Security Lead, Product Owner)
**Required**: Either:
- Named actual people for each gate owner, OR
- Explicit "OWNER UNASSIGNED — GATE CANNOT CLOSE" for unassigned gates
- User should not need to name every person immediately, but plan must not imply approval path exists when it does not

### 12. Risk Register Expansion (Complete Rewrite Required)
**Current state**: Only 4 generic risks
**Required**: At minimum 20+ specific risks:
- Incorrect evidence interpretation
- Unsupported demographic inference
- Live provider fallback
- Duplicate paid calls
- Supplier silence
- Stale inventory and rates
- Proposal/pricing drift
- Cross-tenant leakage
- Malicious inventory uploads
- Document extraction failure
- Invalid model schemas
- Prompt injection
- Provider cost spikes
- Worker restart duplication
- Partial deployment
- Migration incompatibility
- Missing owner approval
- Incomplete measurement data
- Legal and POPIA approval delays
- Maps, email, payment, identity provider delays
- Each with: likelihood, impact, mitigation, detection, contingency, owner

### 13. Coverage Matrix (Complete Rewrite Required)
**Current state**: No coverage matrix
**Required**: Complete coverage matrix proving:
- Every v1.1 specification requirement appears in a gate
- Every requirement has corresponding test
- Every test has retained evidence item
- Every gate has completion criteria
- Every gate has owner approval
- Every gate has GO/NO-GO decision

### 14. Formatting Fixes (Complete Rewrite Required)
**Current state**: Escaped Markdown throughout (\*\*, \-, 1\.)
**Required**: Clean Markdown formatting throughout

## Rewrite Approach

This is not a single-document fix but a systematic rewrite requiring:

1. **Create separate detailed documents** for each major component:
   - Complete agent registry (11 agents × full contracts)
   - Complete tool registry (10+ tools × full contracts)
   - Complete screen register (30+ screens × full contracts)
   - Complete gate specifications (Gates 4-13 × full detail)

2. **Update main implementation plan** to reference these detailed documents rather than containing stubs

3. **Add coverage matrix** linking specification requirements to gates, tests, and evidence

4. **Fix all formatting issues** throughout

5. **Assign or explicitly mark as unassigned** all gate owners

## Estimated Scope

This rewrite is substantial:
- 11 agent contracts × ~20 fields each = ~220 specifications
- 10+ tool contracts × ~15 fields each = ~150 specifications
- 30+ screen contracts × ~15 fields each = ~450 specifications
- 10 gate specifications × ~15 sections each = ~150 sections
- 20+ risk specifications × ~8 fields each = ~160 specifications
- Coverage matrix: ~100 specification requirements mapped

**Total**: ~1,200+ detailed specifications to create

## Current Recommendation

**Do not proceed with implementation** until rewrite is complete. Current plan has sound philosophy but insufficient detail for execution.

**Next steps**:
1. Acknowledge rewrite scope
2. Prioritize most critical components (agent registry, tool registry, screen register)
3. Create detailed documents systematically
4. Update main plan to reference detailed documents
5. Complete coverage matrix
6. Assign actual owners or mark as unassigned
7. Only then consider GO/NO-GO decision

## Current Verdict

**Plan quality**: Strong philosophy and guardrails
**Plan completeness**: Insufficient for execution
**Readiness for implementation**: NOT READY - requires complete rewrite
**Estimated rewrite effort**: Multi-week effort for systematic completion