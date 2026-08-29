# 22. Agent, tool and provider contracts

**Closed roster:** The production roster is the eleven agents already named in Section 6. Do not create another agent to solve an orchestration, validation, extraction, calculation, rendering, notification or state-transition problem. Additions require evidence that none of the existing specialists or deterministic services owns the decision, plus an approved ADR.

## 22.1 Common invocation envelope

| **Group**      | **Fields**                                                                                 | **Rule**                                                         |
|----------------|--------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| identity       | tenantId, actorId, effectiveRole, runId, stepId, correlationId                             | Required and independently authorised by every tool              |
| agent          | agentCode, contractVersion, promptVersion                                                  | Must resolve from the code-controlled agent registry             |
| inputs         | resourceRefs with exact version IDs, approvedEvidenceItemIds, locale, accountPolicyVersion | No floating 'latest' reference after dispatch                    |
| toolPolicy     | allowedTools, maxToolCalls, consequencePolicy                                              | Deny by default; external effect requires human approval         |
| providerPolicy | provider, model, temperature/policy, timeout, maxAttempts, costCapMinor, allowLive         | Deterministic provider is the test default                       |
| resume         | checkpointId?, priorValidatedOutputRef?, priorUsageRef?                                    | Reuse validated work and do not repeat paid calls without policy |

## 22.2 Common output envelope

| **Field**           | **Meaning**                                                       | **Validation**                                             |
|---------------------|-------------------------------------------------------------------|------------------------------------------------------------|
| schemaVersion       | Output contract version                                           | Required                                                   |
| status              | COMPLETED, REVIEW_REQUIRED or FAILED                              | Never infer success from text                              |
| artifact            | Agent-specific typed object                                       | Schema-valid before persistence                            |
| evidenceBindings    | Output field/claim to EvidenceItem IDs                            | Required for material factual claims                       |
| unknowns            | Facts not established by evidence                                 | Must remain explicit; never filled by confidence alone     |
| assumptions         | Reasoned planning assumptions with impact and validation need     | Visibly labelled and reviewable                            |
| confidence          | Per field/claim or decision, not one decorative global score      | Calibrated by evaluation corpus                            |
| objections          | Severity, affected field, evidence gap and recommended resolution | Immutable once attached to a submitted version             |
| rationale           | Concise business explanation                                      | No private chain-of-thought or hidden reasoning transcript |
| suggestedNextAction | One valid workflow command or human task                          | Cannot bypass lifecycle guards                             |
| usage               | Provider/model, units, incremental cost and cache/reuse status    | Written to AIUsageLedger                                   |

## 22.3 Per-agent contract matrix

| **Agent code**           | **Required inputs**                                                       | **Typed output**                | **Allowed tools**                                                                                                                          | **Forbidden**                                                                    |
|--------------------------|---------------------------------------------------------------------------|---------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------|
| opportunity_intelligence | Approved EvidenceSet + reviewed business interpretation                   | OpportunityAngleSet             | Read evidence; retrieval; no external search unless approved capture task                                                                  | Cannot select angle or create Brief approval                                     |
| business_interpretation  | Approved website/file evidence                                            | BusinessInterpretation          | Read evidence; request permitted capture; retrieval                                                                                        | Cannot invent demographics, transaction data or affluent segments                |
| strategy                 | Approved evidence, interpretation and selected opportunity angle          | StrategyDraft                   | Read evidence; approved research sources; benchmark summaries                                                                              | Cannot approve strategy or choose paid inventory                                 |
| brief_drafting           | Approved StrategyVersion, opportunity and evidence                        | BriefDraft                      | Read canonical records; propose new BriefVersion                                                                                           | Cannot reduce Brief fields, silently omit unknowns or overwrite approved version |
| audience                 | Approved BriefVersion, StrategyVersion and evidence                       | AudienceDefinitionSet           | Read evidence and licensed audience adapter if configured                                                                                  | Cannot present hypotheses as confirmed demographics                              |
| inventory_intelligence   | Approved brief/audience/mix plus verified inventory snapshot              | InventoryShortlistDraft         | search_inventory; interpret_product_purpose; evaluate_inventory_eligibility; calculate_commercial_benchmark; geography/route/POI resolvers | Cannot include ineligible, unpriced or materially stale supply as confirmed      |
| media_planning           | Approved brief, audience, media mix, shortlist, rates and supply forecast | MediaMixDraft or MediaPlanDraft | Search/read inventory; benchmark; forecast; deterministic calculator                                                                       | Cannot alter brief, fabricate reach or approve plan                              |
| critic_readiness         | Candidate artefact, input versions, evidence and policy                   | CriticReport                    | Read-only access to canonical artefacts and evidence                                                                                       | Cannot edit artefact, downgrade severity silently or approve                     |
| proposal_narrative       | Approved brief and media plan plus account proposal policy                | ProposalNarrativeDraft          | Read approved facts; request deterministic render preview                                                                                  | Cannot change totals, inventory, terms, evidence or send externally              |
| creative                 | Approved brief/strategy/plan and format specifications                    | CreativeConceptSet              | Read brand assets and format requirements                                                                                                  | Cannot publish, imply clearance or alter booked format                           |
| measurement              | Verified delivery/performance evidence and approved measurement plan      | MeasurementInterpretation       | Read metrics/evidence; deterministic comparisons                                                                                           | Cannot claim causality beyond measurement design or autonomously optimise spend  |

*Audience reasoning must consider objective, product and price context, buying occasion, geography, language, age/life-stage, LSM/SEM where supported, budget, dates, channel contribution and evidence quality. The agents must not use the blanket statement 'demographics cannot be confirmed from website evidence' when credible commercial inferences can be made; they must separate confirmed facts from labelled inferences and questions. Sensitive attributes such as race may be used only as lawful aggregate planning evidence with source, purpose and review, never inferred for an individual.*

## 22.4 Runtime, prompt and cost policy

- Prompts contain the task, typed input, allowed tools, output schema, evidence rules, human-gate rules and business-language requirement. They do not contain secrets or broad repository context.

- The runtime validates tool arguments before dispatch and validates output before persistence. A schema repair may use deterministic normalization; semantic repair requires a recorded provider attempt.

- Live/paid Bedrock is disabled throughout redevelopment and production certification. The local deterministic provider must exercise identical contracts, schemas, tools, checkpoints, errors, audit and usage semantics. A first live call requires separate explicit owner approval after greenlight.

- The default provider budget is set per workflow and account. The runtime checks remaining budget before each call and fails safely with COST_POLICY_BLOCKED when exceeded.

- If a timeout occurs after a provider may have accepted work, do not automatically repeat the paid call. Reconcile provider/request identifiers or require a human resume decision.

- Provider, model, prompt or schema changes require golden-corpus evaluation, cost comparison, rollback plan and controlled rollout. Model output is never deployed solely because it looks plausible in one example.

## 22.5 Agent evaluation release gate

| **Dimension** | **Release threshold**                                                                                   | **Evidence**                                                             |
|---------------|---------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|
| Contract      | 100% schema-valid outputs and stable error classification                                               | Every agent/provider combination                                         |
| Evidence      | 100% of material factual claims are evidence-bound or explicitly labelled assumption/unknown            | Golden corpus plus adversarial cases                                     |
| Safety        | Zero unapproved external action, cross-tenant access or direct database mutation                        | Negative tool and permission tests                                       |
| Quality       | Owner-approved rubric threshold for correctness, usefulness, objection handling and business language   | Per-agent labelled evaluation set                                        |
| Regression    | No material degradation versus current approved prompt/provider baseline                                | Automated comparison before rollout                                      |
| Cost          | Certification records zero live/paid calls; resume tests prove no duplicate attempt or simulated charge | Deterministic replay; any later live canary is separately owner-approved |
| Recovery      | Retry, review-required, timeout, provider failure and manual continuation all reach a stable state      | Integration and Playwright journeys                                      |

# 23. Inventory and evidence implementation specification

## 23.1 Supplier-agnostic import pipeline

| **Step** | **Stage**         | **Required behaviour**                                                                                       | **Output**                        |
|----------|-------------------|--------------------------------------------------------------------------------------------------------------|-----------------------------------|
| 1        | Receive           | Create upload intent; validate membership, size, type, hash and supplier context                             | InventoryImport.UPLOADED          |
| 2        | Protect           | Malware scan, object quarantine and content-type verification                                                | safe object or terminal rejection |
| 3        | Classify          | Detect PDF, XLSX, CSV, DOCX, image and document class without trusting filename                              | document class + confidence       |
| 4        | Render            | Create page/sheet images and coordinate map for human-equivalent inspection                                  | render manifest                   |
| 5        | Extract structure | Use Docling/table reconstruction to preserve headings, cells, merged ranges and coordinates                  | structured document               |
| 6        | Extract assets    | Recover logos, OOH photographs and other embedded media with source coordinates                              | asset candidates                  |
| 7        | Normalize         | Map rows to common and channel schemas; keep raw value, normalized value and transformation                  | InventoryCandidates               |
| 8        | Link evidence     | Attach file, page/sheet, range/cell/box and excerpt to every commercial field                                | field-level lineage               |
| 9        | Validate          | Check required fields, money, VAT, dates, units, geography, duplicates and channel invariants                | errors/warnings                   |
| 10       | Enrich            | Resolve known supplier/station/publication identity and approved reference geography; never replace evidence | enrichment proposal               |
| 11       | Review            | Show original/render beside candidate, changes, confidence and issues                                        | append-only decisions             |
| 12       | Publish           | Commit approved products, versioned rates, availability, assets and evidence idempotently                    | published inventory               |
| 13       | Benchmark readiness | Preserve typed comparable attributes, spatial keys, rate basis and freshness needed by downstream intelligence; Gate 6 makes no market-position claim | benchmark-ready published truth   |
| 14       | Evaluate          | Run labelled known and unseen-file corpus; record precision, recall, unresolved rate and failure class       | release report                    |

## 23.2 Common inventory schema

| **Field group**      | **Canonical fields**                                                                                                                  |
|----------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| Identity             | supplierId, supplierProductCode?, channelCode, productTypeCode, name, description, status                                             |
| Geography            | country, province, municipality, locality, address?, latitude?, longitude?, geometry?, coverageArea?                                  |
| Commercial           | rateTypeCode, amountMinor, currency, vatStatus, commission, inclusions, exclusions, productionCost?, installationCost?, minimumOrder? |
| Timing               | validFrom, validTo, availabilityWindow, bookingLeadTime, cancellationTerms                                                            |
| Audience/measurement | audienceSource?, audiencePeriod?, reach?, impressions?, circulation?, traffic?, methodology?, limitations\[\]                         |
| Assets               | logo, productImages\[\], specifications\[\], termsDocument?, rateCardSource                                                           |
| Evidence             | sourceId, fieldLocators, capturedAt, reviewedAt, reviewer, verificationLevel, confidence                                              |
| Freshness            | rateConfirmedAt, availabilityConfirmedAt, expiresAt, staleReason?, supplierConfirmationStatus                                         |

## 23.3 Channel extension schemas

| **Channel**         | **Required extension fields**                                                                                                                                       |
|---------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| OOH/DOOH            | format, dimensions, structureType, illumination, digital, loopLength, slotLength, playsPerLoop, trafficDirection, road, route, POIs, production/installation, image |
| Radio               | station, frequency, broadcastArea, packageType, daypart, days, spotLength, spots, programme, sponsorship, powerspot, audienceSource                                 |
| Television          | channel, programme, daypart, days, spotLength, spots, package, sponsorship, audienceSource, materialDeadline                                                        |
| Print               | publication, edition, frequency, section, placement, size, colour, insertionCount, circulationSource, materialDeadline                                              |
| Digital/social      | publisher/platform, placement, format, buyingUnit, targeting, estimatedImpressions, CPM/CPC/fixed rate, creativeSpecs, tracking                                     |
| Influencer          | profile, platform, handle, representedBy, audienceSnapshot, deliverableType, quantity, usageRights, exclusivity, production, rate                                   |
| Experiential        | venue/asset, capacity, footprint, duration, staffing, equipment, permits, inclusions, exclusions, production, rate                                                  |
| Podcast/audio       | show/network, episode placement, hostRead, duration, downloadsSource, targeting, production, usage, rate                                                            |
| Retail/transit/mall | network, venue, unitType, footfallSource, dimensions, dwellContext, digitalLoop, installation, rate                                                                 |
| Email/mobile        | publisher/list owner, format, audience basis, volume, delivery unit, targeting, creativeSpecs, privacy basis, rate                                                  |

## 23.4 Publication and unseen-file gates

| **Gate**         | **Rule**                                                                                                | **Disposition**                              |
|------------------|---------------------------------------------------------------------------------------------------------|----------------------------------------------|
| Price            | Amount, currency, VAT status, rate type, valid period and source locator exist                          | Block publish                                |
| Product identity | Supplier, channel, product type and sellable name are resolved                                          | Block publish                                |
| Availability     | May be unknown but must be explicitly UNKNOWN and confirmed before booking                              | Warn/booking block                           |
| Geography        | Required channel geography validates; OOH coordinates checked where supplied                            | Block affected product                       |
| Assets           | Missing logo/image creates a visible review task; placeholder never presented as verified asset         | Review required                              |
| Audience claims  | Source, period and methodology recorded; otherwise claim is unverified and excluded from client promise | Block claim                                  |
| Terms            | Material inclusions, exclusions, deadlines and cancellation terms retained                              | Review required                              |
| Precision        | Critical commercial-field precision \>=99% on labelled supported corpus                                 | Release block                                |
| Recall           | Sellable-product and critical-field recall \>=95% on labelled supported corpus                          | Release block or documented class limitation |
| Unseen files     | At least one held-out supplier per supported document class completes without document-specific code    | Release block                                |

- Tests are named for business behaviour and document class, not implementation slices. Remove test_slice\*, numbered slice fixtures and production data labels that expose development history.

- A parser exception for one supplier is permitted only as a declarative mapping/configuration backed by a general document-class rule and regression fixture; do not add supplier-name conditionals to core extraction.

- The review screen must support field correction, multi-row action, evidence zoom, asset replacement, duplicate resolution, rejection reasons and publish preview for catalogues above 10,000 products.

## 23.5 OOH/DOOH benchmark engine

The benchmark engine runs only against published, permissioned inventory truth. It is deterministic first and may optionally add an AI explanation after the facts are calculated. A benchmark used by a shortlist, plan or proposal is persisted as an immutable snapshot bound to exact product/rate versions and a benchmark-policy version.

### 23.5.1 Comparator cohort rules

A target OOH/DOOH placement is compared only with products that pass all hard compatibility checks. Defaults are governed configuration, not prompt text or UI-only logic.

| **Dimension** | **Hard rule / default behaviour** |
|---------------|-----------------------------------|
| Publication state | Target and peers are published and not superseded/withdrawn for the comparison period. |
| Channel | OOH compares with OOH; DOOH compares with DOOH. Digital and static are never silently mixed in one price cohort. |
| Product/format class | Same canonical product type and compatible governed format/structure class. Broader fallbacks must be labelled and may lower confidence. |
| Rate basis | Same buying unit or a documented deterministic normalization to a common unit. Incompatible package, per-play, loop, weekly, four-week and monthly rates are excluded rather than guessed. |
| Currency/VAT | Common currency and VAT basis before statistics. FX conversion is not implicit; if configured later it must use a dated evidenced rate and remain visible. |
| Effective period | Rate validity overlaps the requested comparison date/window. Expired historical rates can be shown as history but not mixed into a current-market benchmark. |
| Geography | Prefer verified coordinates and PostGIS distance. Progressively expand configured radii only until the target cohort size is reached; then locality and municipality fallbacks may be used and are explicitly labelled. |
| Physical size | Where dimensions exist, compare within a governed display-area tolerance or matching size/format band. Missing dimensions lower confidence and cannot be treated as equal size. |
| Digital delivery | Loop length, slot length, plays per loop and share-of-loop must be compatible or deterministically normalizable before commercial efficiency comparison. |
| Measurement | Traffic/reach/impressions/footfall efficiency is calculated only when source, period, unit and methodology are compatible. |
| Freshness | Stale rates remain visible as exclusions/history unless policy explicitly permits them; stale facts do not silently influence current-market position. |

Default spatial expansion should be configurable, with an initial policy such as 3 km → 5 km → 10 km → 25 km → locality → municipality. The engine stops widening when it has a useful compatible cohort; it does not keep adding distant products merely to make the sample look larger.

### 23.5.2 Required deterministic outputs

For every successful benchmark the service returns at minimum:

- target product version, rate version, normalized buying unit and comparison date/window;
- actual geography basis used, target coordinate, peer distance for spatial cohorts and radius/fallback level;
- included peer product/rate version IDs and excluded candidate counts grouped by stable reason code;
- comparable peer count and data-completeness/freshness summary;
- target rate, peer median, lower/upper quartile, minimum, maximum and target rate percentile;
- percentage and absolute amount above/below the peer median;
- normalized rate per display-area unit when dimensions and buying basis support it;
- verified cost-per-thousand or equivalent audience/traffic efficiency only when compatible measurement evidence exists;
- confidence classification plus machine-readable reasons such as `SMALL_COHORT`, `MISSING_DIMENSIONS`, `MIXED_MEASUREMENT_METHOD`, `STALE_PEERS` or `GEOGRAPHY_FALLBACK`;
- the exact benchmark-policy version, normalization rules and calculation timestamp.

Do not calculate a supposedly precise market percentile from an insufficient cohort. Governed defaults should treat fewer than 2 compatible peers as `INSUFFICIENT`; 2–4 as low confidence; 5–9 as medium confidence; and 10+ as high sample-size confidence, with the final confidence also reduced by freshness, missing fields and geography/measurement fallbacks. These thresholds are configuration and must be versioned if changed.

### 23.5.3 Human-facing market position

The product detail experience must include a `Market comparison` section that makes the calculation understandable without exposing implementation jargon. At minimum it shows:

- the target rate and normalized basis, for example `R42,000 per 4 weeks`;
- peer median and the target's percentage above/below it;
- number of comparable sites and the actual area used, for example `18 comparable digital large-format sites within 5 km`;
- percentile or quartile position when statistically defensible;
- measurement-efficiency comparison when evidence permits it;
- rate freshness and benchmark confidence;
- a governed plain-language position such as `strong value`, `market-aligned` or `above market`, derived from visible deterministic thresholds rather than an opaque AI score;
- a `View comparable sites` action opening a map/list of the target and included peers with rate, format, size, distance, freshness and source/verification context;
- an explanation panel showing the cohort filters and why material near-by products were excluded.

An optional AI narrative may explain the result, e.g. that a placement is below the local median while carrying stronger verified traffic, but it receives only the benchmark facts. It cannot alter the cohort, perform hidden arithmetic, invent audience measures or turn `INSUFFICIENT`/low-confidence evidence into a confident recommendation.

### 23.5.4 Reproducibility and downstream use

- Interactive product-detail benchmarks may be recalculated against the latest published truth, but any benchmark cited by Inventory Intelligence, a shortlist, MediaPlanVersion or ProposalVersion stores an immutable `InventoryBenchmarkSnapshot`.
- If a peer rate, target rate, relevant product attribute or benchmark policy changes, the live benchmark becomes a new calculation and affected draft planning artefacts are marked stale where the change is material.
- Supplier ownership, commercial preference or campaign fit may influence shortlist scoring only after the benchmark facts are calculated; they cannot change the market statistics themselves.
- Benchmark outputs are decision support, not proof of performance. `Cheaper` means cheaper on the stated comparable commercial basis; `better value` requires the additional evidenced metric(s) displayed beside that conclusion.

# 24. Authenticated screen implementation contracts

## 24.1 Universal screen contract

| **Element**      | **Required behaviour**                                                                                          |
|------------------|-----------------------------------------------------------------------------------------------------------------|
| Purpose          | One user-recognisable outcome stated in commercial language                                                     |
| Permission       | Route guard plus API enforcement; forbidden screen explains access without revealing resource existence         |
| Header           | Page title, client/campaign context, plain-language status and responsible owner                                |
| Primary action   | One dominant next action valid for the current role and state; secondary actions are visually subordinate       |
| Data states      | Loading skeleton, empty state, partial/stale state, success, validation error, service failure and recovery     |
| Decision support | Evidence, assumptions, confidence, price freshness, rejection reasons and comparison where relevant             |
| Mutation         | Confirmation for consequential actions, idempotent submission, progress, success result and retry-safe recovery |
| Navigation       | Back returns to the previous business context; stepper reflects canonical lifecycle, not arbitrary pages        |
| Accessibility    | Keyboard path, visible focus, semantic headings/tables, labels, contrast, alt text and screen-reader status     |
| Telemetry        | Page view, primary action, validation failure, exception and recovery event with no unnecessary PII             |

## 24.2 Critical screen acceptance matrix

| **Route**                     | **User outcome**                                                       | **Primary action**                | **Required exceptional states**                                                                        |
|-------------------------------|------------------------------------------------------------------------|-----------------------------------|--------------------------------------------------------------------------------------------------------|
| /home                         | Role outcome summary and priority queue                                | Open highest-priority task        | Empty queue, stale KPI, partial service, forbidden item removed                                        |
| /briefs/new                   | Upload or paste the original client brief before interpretation        | Understand this brief             | File/text source, parse failure, missing source, Advertified determines Rapid OOH versus full campaign |
| /opportunities                | Find, filter and create opportunities                                  | Create opportunity                | Empty, no results, import/capture pending                                                              |
| /opportunities/:id            | Understand evidence, selected angle, lifecycle and next decision       | Complete current gate             | Capture failure, evidence review, agent failure, lost/archived                                         |
| /briefs/:id                   | Review the complete canonical brief and version differences            | Submit or approve brief           | Draft, review, rejected, approved, stale downstream                                                    |
| /strategies/:id               | Assess recommendation, evidence and critic objections                  | Approve or request changes        | Unsupported claim, unresolved objection, new evidence                                                  |
| /audiences/:id                | Review evidence-backed audience definitions                            | Approve audience direction        | Hypothesis labels, exclusions, low confidence                                                          |
| /media-mixes/:id              | Compare channel roles and budget allocation                            | Approve media mix                 | Budget mismatch, unsupported channel, revision                                                         |
| /inventory                    | Search large verified catalogue                                        | Select/view product               | No match, stale rates, unavailable, partial assets                                                     |
| /inventory/intelligence       | Compare verified inventory and understand local commercial position    | Open benchmarked product          | Insufficient peers, low confidence, geography fallback, stale/incompatible rates                       |
| /inventory/:id                | Dedicated editable product detail with transparent market comparison   | View comparable sites             | Rate history, asset/evidence review, insufficient peers, low confidence, supplier-only fields          |
| /inventory/imports/:id/review | Resolve extraction candidates beside source evidence                   | Publish approved inventory        | 10k+ pagination, errors, duplicates, missing assets                                                    |
| /shortlists/:id               | Understand selected and rejected inventory                             | Confirm shortlist                 | Replace inventory, supplier confirmation, no eligible supply                                           |
| /media-plans/:id              | Inspect lines, forecasts, rates, totals and assumptions                | Approve media plan                | Stale rate, availability unknown, total mismatch                                                       |
| /proposals/:id                | Review distinct client options and branded preview                     | Approve proposal                  | Draft, stale plan, render failure, expired                                                             |
| /proposals/:id/send           | Confirm exact recipient and approved version                           | Send proposal                     | No recipient, expired approval, duplicate send protected                                               |
| /proposals/:id/funding        | Record selected option, signed PO and payment route                    | Submit PO or choose payment route | VodaPay, manual EFT, Advertise Now Pay Later, reconciliation pending                                   |
| /supplier/requests/:id        | Respond to RFQ or booking with evidence                                | Submit response                   | Deadline passed, partial availability, revised price                                                   |
| /campaigns/:id                | Track booking, creative, delivery and outcomes                         | Complete current delivery task    | Missing creative, proof rejected, delayed delivery                                                     |
| /runs/:id                     | Explain workflow progress, evidence, failure and cost                  | Resume safe checkpoint            | Waiting for human, provider blocked, not resumable                                                     |
| /tasks                        | Show only meaningful assigned decisions                                | Open/complete task                | Empty, overdue, reassigned, resource unavailable                                                       |
| /agent-operations             | Inspect real persisted run stages, tools, checkpoints, errors and cost | Open exception or safe resume     | No mock dispatch, fake progress or hidden endpoint                                                     |

## 24.3 Human-facing content and visual guardrails

| **Rule**                   | **Implementation requirement**                                                                                                                                             |
|----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Brief input                | The user supplies or confirms a campaign brief. Do not ask for implementation parameters, schema fields or agent settings.                                                 |
| Plain language             | Say what Advertified understood, what evidence supports it, what remains uncertain and what the user can do next.                                                          |
| Forbidden internal wording | Do not show clean application, browser boundary, canonical aggregate, runtime, schema, dispatch, payload, provider or migration terminology to ordinary users.             |
| Confidence                 | Use specific phrases such as Confirmed from supplied rate card, Supplier confirmation needed or Planning assumption - not vague confidence theatre.                        |
| Errors                     | Explain the affected business action and recovery. Keep correlation ID available under technical details for support.                                                      |
| Visual system              | Navy, white, neutral grey and electric blue remain the interface system. Dark green is prohibited.                                                                         |
| Layout                     | Readable type, restrained density, no action in the middle of unfinished content, no repeated banners and no decorative stock-image dependency.                            |
| Truthful progress          | Steppers and activity show persisted backend stages/signals only. Never fabricate percentages, discoveries, provider calls, publication, supplier responses or completion. |
| Validation                 | A failed action opens the correct step, focuses the first invalid visible field and explains every blocker. Hidden required inputs may not prevent progress.               |
| Catalogue scale            | For 10,000+ products use server hierarchy, search, filters, grouping, counts, cursor pagination and virtualization. The browser never loads the full catalogue.            |
| Route scope                | Authenticated product only. Do not add public/marketing pages or unsupported destinations. Future capabilities remain visibly disabled with a reason.                      |
| Reference fidelity         | Where approved screen references exist, compare at the identical Playwright viewport. Passing render/smoke tests alone does not prove visual fidelity.                     |
| No decorative duplication  | Media Planning is a real cross-campaign queue; Audiences and Budgets stay campaign-scoped; Creatives connects to assets; Insights does not duplicate Measurement/Reports.  |
| Responsive                 | Desktop-first planning surfaces remain usable at 1280px; approvals and task actions remain usable on small screens.                                                        |

## 24.4 Dashboard KPI contracts

| **Dashboard**             | **KPIs**                                                                     | **Scope**                               |
|---------------------------|------------------------------------------------------------------------------|-----------------------------------------|
| Internal operations       | Open approvals, agent exceptions, active plan value, overdue tasks           | Assigned work and operational risk      |
| Agency                    | Active briefs, proposals due, on-time rate, planned client spend             | Assigned advertiser portfolio           |
| Advertiser                | Active campaigns, approvals required, planned spend, delivery status         | Own organisation                        |
| Supplier                  | Live listings, open requests, fresh-rate percentage, confirmed booking value | Own supplier inventory and transactions |
| Influencer/representative | Open requests, deliverables due, pending value, proof status                 | Owned or represented profiles           |

# 25. Durable jobs, events, human tasks and notifications

## 25.1 Job execution contract

| **Concern**   | **Required implementation**                                                                            |
|---------------|--------------------------------------------------------------------------------------------------------|
| Claim         | Worker claims queued job with lease and SKIP LOCKED/equivalent; heartbeat extends lease                |
| Checkpoint    | Each completed step stores validated output reference and input hash before the next side effect       |
| Idempotency   | Every side effect uses stable key derived from tenant, command/resource version and effect type        |
| Retry         | Transient operations: 30 seconds, 2 minutes, 10 minutes; then review/dead-letter with visible recovery |
| Paid provider | Do not retry when provider acceptance/cost is ambiguous; reconcile or require human resume             |
| Cancellation  | Stop future steps, preserve completed artefacts and never pretend an external effect was undone        |
| Recovery      | Expired lease is reclaimable; worker restart resumes from checkpoint; duplicate delivery is harmless   |
| Poison job    | Retain input, classification, last error, attempts and owner; do not spin indefinitely                 |

## 25.2 Canonical business events

| **Event**                | **Aggregate**         | **Required consumer outcome**                                               |
|--------------------------|-----------------------|-----------------------------------------------------------------------------|
| EvidenceApproved         | EvidenceItem          | Refresh affected readiness and unblock eligible agent step                  |
| StrategyApproved         | StrategyVersion       | Enable Brief draft/revision task                                            |
| BriefApproved            | BriefVersion          | Enable audience/media mix planning                                          |
| MediaMixApproved         | MediaMixVersion       | Enable shortlist and supply workflow                                        |
| InventoryRateChanged     | InventoryRate         | Mark affected draft plans stale and notify owner                            |
| AvailabilityChanged      | InventoryAvailability | Re-evaluate affected shortlist/plan and create confirmation task            |
| MediaPlanApproved        | MediaPlanVersion      | Enable proposal generation                                                  |
| ProposalApproved         | ProposalVersion       | Enable render/send task; never send automatically                           |
| ProposalTierSelected     | ProposalVersion       | Create booking workflow                                                     |
| SupplierResponseReceived | SupplierResponse      | Create review task and invalidate changed assumptions                       |
| BookingConfirmed         | Booking               | Advance campaign readiness                                                  |
| CreativeApproved         | CreativeAsset         | Advance campaign when all required formats approved                         |
| DeliveryProofSubmitted   | DeliveryProof         | Create proof review task                                                    |
| AgentRunReviewRequired   | AgentRun              | Create one actionable human task with evidence and recovery                 |
| AgentRunCompleted        | AgentRun              | Attach artefact and usage ledger; advance only when lifecycle guard permits |

## 25.3 Human task contract

- A task represents a real decision or exception, not a link whose only purpose is to open another page.

- Every task has a business title, why it matters, affected resource/version, evidence or comparison, one primary action, allowed alternatives, due date, assignee and recovery state.

- Completing a task calls a typed Commercial API command. Closing the screen, refreshing or clicking twice cannot duplicate the decision.

- Reassignment, escalation and overdue notification are audited. A completed task is immutable except for an explicit correction workflow.

## 25.4 Notification policy

| **Trigger**             | **Recipient**                                           | **Channel**                                                  | **Control**                                    |
|-------------------------|---------------------------------------------------------|--------------------------------------------------------------|------------------------------------------------|
| Approval requested      | Named approver                                          | In-app immediately; email when account policy permits        | One reminder before due; escalation after due  |
| Supplier request        | Verified supplier contact                               | Human-confirmed external email and supplier inbox            | No automatic resend after ambiguous delivery   |
| Proposal ready/send     | Internal owner then resolved client recipient           | In-app approval; external email only after send confirmation | Idempotency prevents duplicates                |
| Rate/availability stale | Plan owner and inventory ops                            | In-app; digest email for non-urgent items                    | Escalate if proposal/booking is blocked        |
| Run failed/review       | Assigned owner                                          | In-app immediately; operational alert for systemic failure   | Do not expose provider internals to clients    |
| Booking/creative/proof  | Assigned internal, supplier and advertiser participants | Role-appropriate in-app/email                                | Message only the state each recipient may view |

