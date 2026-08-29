# Creative Intelligence design — proposal concepts and final production

**Design date:** 2026-08-29  
**Status:** DESIGN ONLY — future-gate preparation; not authorised implementation  
**Current gate:** Gate 6 inventory truth is in progress  
**Purpose:** Define the smallest coherent PDF/catalogue → verified brand/product assets → illustrative creative concepts → proposal flow without touching active Gate 6 work.

## 1. Required distinction

Advertified already defines one `creative` agent, `CreativeConceptSet`, `CreativeAsset`, creative approval and campaign delivery states. The missing distinction is lifecycle context:

1. **Creative Intelligence** — proposal-stage, non-publishable concepts that show how an approved strategy could come to life.
2. **Creative Production** — post-booking final assets bound to exact booked formats, rights, approvals and publication controls.

Do not add another agent. The existing `creative` specialist remains the AI owner for creative interpretation. Deterministic services own source handling, lineage, validation, commercial text, rendering, lifecycle and approvals.

## 2. Existing invariants remain unchanged

- Commercial API owns canonical state, authorisation, lifecycle, versioning, audit and approvals.
- AI proposes typed output and never publishes, approves, spends, books or communicates externally.
- Creative agent reads approved Brief/Strategy/Plan plus brand assets and format requirements.
- Creative/public publication requires named human approval and an exact asset version.
- Personalized production at scale remains deferred until brand, legal and asset pipelines exist.
- Campaign delivery still uses `BOOKED -> CREATIVE_PENDING -> READY` for final production assets.

## 3. Lifecycle placement

### Proposal-stage Creative Intelligence — Gate 8

```text
Approved BriefVersion
→ approved audience/media-plan inputs
→ CreativeConceptSet draft
→ deterministic validation
→ human review
→ illustrative concepts attached to ProposalVersion
→ client decision
```

Concepts are explanatory proposal material only. Every preview must clearly state that it is illustrative and that final offers, pricing, artwork and publication remain subject to approval. Exact wording belongs in governed content resources when implemented.

### Delivery-stage Creative Production — Gate 11

```text
Accepted Proposal
→ bookings confirmed
→ Campaign BOOKED
→ RequestCreative
→ CREATIVE_PENDING
→ exact-format CreativeAsset versions
→ required human approvals
→ READY
→ publication/delivery
```

A proposal preview may inform final production but cannot silently become an approved live asset. Final production creates new exact versions with rights, format and approval evidence.

## 4. Client source material

Creative Intelligence needs a client-side source boundary separate from supplier inventory ingestion. Examples include:

- product catalogue PDF;
- product brochure;
- logo files;
- product photography;
- brand guidelines;
- current specials or price lists;
- previous approved campaign examples;
- supplied copy or legal wording.

A client catalogue is client evidence and creative source material. It must not be forced through the inventory domain merely because Gate 6 accepts PDFs.

### Proposed source concept

```text
ClientSourceDocument
- id
- tenant_id
- client_id
- source_type
- original_filename
- declared_media_type
- detected_media_type
- object_key
- content_hash
- captured_at
- supplied_by
- rights_status
- lifecycle_status
```

This is a design shape, not an authorised migration. The immutable source file remains the evidence anchor. Every derived asset retains source document, page/region locator and source hash.

## 5. Brand and product asset extraction

A future deterministic **Client Asset Extraction** service should create reviewable derived assets from source documents. It should attempt to identify:

- logos and brand marks;
- product photographs and product-group compositions;
- background/texture references and useful supplied icons;
- product names and category labels;
- offer/price text and validity dates;
- contact/CTA information;
- visual references such as palette and layout characteristics.

It must not infer a sellable product merely because an image resembles one. Product identity and commercial claims require source evidence or human correction.

### Proposed derived asset concept

```text
ClientCreativeAsset
- id
- tenant_id
- client_id
- source_document_id
- asset_type
- object_key
- content_hash
- source_locator
- rights_status
- review_status
- created_at
```

Reuse governed asset codes such as `LOGO`, `PRODUCT_IMAGE` and `CREATIVE_FILE` where semantically correct. Do not introduce supplier-specific codes.

### Product facts and association

```text
ClientProductFact
- product_name
- category
- source_document_id
- source_locator
- current_price_minor?
- previous_price_minor?
- currency?
- offer_valid_from?
- offer_valid_until?
- evidence_status
```

A product image may be associated with a product fact only when the source supports it or a human confirms it.

## 6. Commercial-truth rules

If proposed campaign dates fall outside a supplied offer period:

1. retain the offer as historical/source evidence;
2. mark it unavailable for unqualified campaign use;
3. show a human-sensible warning that pricing or offer validity needs confirmation;
4. allow non-price creative when appropriate;
5. never let an image model invent or extend the offer period.

Unknown validity remains unknown. Missing prices remain missing. Generated text never becomes verified commercial truth.

## 7. Brand profile

A future reviewed `BrandProfileVersion` may summarize visual guidance from supplied assets and explicit client settings:

```text
BrandProfileVersion
- client_id
- source_asset_ids[]
- logo_asset_ids[]
- palette[]
- photography_notes[]
- layout_notes[]
- typography_notes[]
- required_elements[]
- prohibited_elements[]
- human_notes[]
- status
- approved_by?
```

AI observations remain proposed notes until reviewed; they do not become binding brand rules by inference alone.

## 8. CreativeConceptSet contract

The existing creative agent should produce a proposal-stage typed result rich enough to explain and render concepts without publication authority.

```text
CreativeConceptSet
- brief_version_id
- strategy_version_id
- media_plan_version_id?
- brand_profile_version_id?
- concept_set_version
- territories[]
- commercial_warnings[]
- evidence_refs[]

CreativeTerritory
- name
- strategic_role
- rationale
- audience_variants[]
- channel_concepts[]
- preferred_asset_ids[]
- required_asset_ids[]
- headline_options[]
- copy_options[]
- cta_options[]
- visual_direction
- exclusions[]

ChannelConcept
- channel
- format
- purpose
- message
- visual_instruction
- source_asset_ids[]
- text_elements[]
```

The contract must distinguish approved facts, model proposals, unknowns and warnings.

## 9. Image generation boundary

Image generation is a provider capability, not canonical state. Introduce a provider-neutral application boundary only when Gate 8 implementation is authorised, for example:

```text
ICreativeImageProvider
- GenerateConceptPreview(request)
- EditConceptPreview(request)
```

Provider configuration remains disabled by default during redevelopment. Deterministic local fixtures must exercise identical contracts until a live provider receives separate owner approval.

Provider requests should use exact approved source assets, required format/aspect ratio, approved concept direction, prohibited changes, product-preservation instructions and brand references. For real supplied products, default behavior is to preserve the product rather than replace it with a generated approximation.

## 10. Deterministic artwork renderer

The image model must not own exact commercial text. A deterministic renderer composes:

```text
creative visual
+ reviewed logo
+ exact headline
+ verified product name
+ approved current price/offer
+ CTA/contact details
+ required disclosure/legal text
= proposal preview
```

This avoids image-model spelling, price and phone-number errors. The renderer also owns channel-safe sizing and layout constraints.

## 11. Critic and validation rules

Reuse `critic_readiness`; do not add a separate critic agent. Checks should identify at least:

- invented/replaced product when a real asset was required;
- unsupported product association;
- expired or unconfirmed pricing;
- wrong/altered logo;
- missing rights status;
- source asset outside tenant/client scope;
- contradiction with approved Brief/Strategy/Plan;
- format mismatch or missing disclosure;
- generated text represented as verified fact;
- proposal concept represented as final approved artwork.

The critic proposes objections; only human disposition advances the concept.

## 12. Proposal integration — Gate 8

Creative Intelligence should be an optional first-class proposal section, not a parallel workflow. A ProposalVersion may reference an exact reviewed `CreativeConceptSetVersion` and deterministic preview assets.

Client-facing order can remain simple:

1. Campaign direction.
2. Audience and media rationale.
3. Creative approach.
4. Selected concept territory.
5. Useful channel examples.
6. Commercial proposal and assumptions.
7. Clear illustrative-concept disclosure.

Creative generation must not block a valid proposal when approved source assets are unavailable. The proposal should state that examples were not generated rather than inventing source material.

## 13. Delivery integration — Gate 11

Final `CreativeAsset` production must bind exact campaign/booking requirements, media specifications, approved copy and commercial values, rights/legal/brand state, source/input versions, human approvals and immutable object hash/version.

A proposal-stage preview cannot satisfy delivery-stage creative approval.

## 14. User experience

Primary screens should use human language and answer:

- What is the idea?
- Who is it for?
- What are we showing or saying?
- Where could it appear?
- Why does it support the campaign?
- What still needs confirmation?

Hashes, provider metadata and internal validation details belong behind evidence/details surfaces, not in client-facing copy.

## 15. Minimal future implementation sequence

Do not implement until the controlling gate is authorised.

### Gate 8

1. Client source-document intake/reuse for proposal evidence.
2. Derived brand/product asset review.
3. BrandProfileVersion.
4. Rich CreativeConceptSet proposal contract.
5. Deterministic image-provider fixture.
6. Deterministic artwork renderer.
7. Proposal preview integration.
8. Human concept review and immutable version binding.

A live image provider remains separately gated.

### Gate 11

1. Exact booked-format creative requirements.
2. Final CreativeAsset versioning.
3. Rights/legal/brand approval state.
4. Delivery-ready validation.
5. Publication/release consequence gate.

## 16. Future Gate 8 acceptance rules

The eventual work packet should prove that:

1. a client PDF remains immutable and every derived asset links to source/page/hash;
2. product images cannot cross tenant/client scope;
3. a supplied logo/product image can be used without regenerating or approximating it;
4. an expired offer cannot render as current without a new approved source/confirmation;
5. missing prices are never fabricated;
6. exact commercial text is rendered deterministically, independent of image generation;
7. provider failure does not corrupt canonical state or prevent recovery;
8. concept previews are explicitly non-publishable;
9. the exact concept version attached to an approved/sent proposal is immutable;
10. proposal concepts cannot satisfy delivery-stage CreativeAsset approval;
11. no live/paid provider is used without separate explicit owner approval;
12. proposal UX remains human-readable on desktop and compact layouts.

Tests should cover only these real acceptance rules, security boundaries, lifecycle invariants and regressions. Do not create broad image-quality snapshot suites or tests of third-party model behavior.

## 17. Explicit exclusions and Gate 6 non-overlap

This design does not authorise:

- changes to Gate 6 files, contracts, migrations or acceptance criteria;
- new database tables or migrations;
- a new creative agent;
- live GPT Image, Bedrock or other paid image calls;
- automatic publication or autonomous legal/brand approval;
- invented client products, offers or prices;
- generative replacement of supplied product truth by default;
- a general DAM/PIM/CMS unrelated to proposal/delivery needs;
- supplier-specific extraction logic;
- implementation of Gate 8 or Gate 11 before gate order permits it.

Gate 6 remains solely responsible for supplier inventory truth:

```text
supplier file
→ protect/classify
→ extract inventory facts/assets
→ human review
→ versioned inventory publication
→ searchable supply
```

Creative Intelligence will later consume approved client/brand/product source material and approved campaign artefacts. It does not modify Gate 6 supplier semantics, inventory assets, publication lifecycle, OpenAPI, migrations or web routes while Gate 6 is active.

This document is safe future-gate preparation because it creates no code, schema, alternate truth or premature integration.
