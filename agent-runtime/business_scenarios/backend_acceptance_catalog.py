"""Data contract and deterministic catalogue for the owner-required 100-case campaign.

This module prepares the backend scenario set. It does not substitute for the later
full end-to-end run after the frontend phase and it never calls a paid provider.
"""
from __future__ import annotations

from typing import Annotated, Any, Literal

from pydantic import Field, NonNegativeInt

from contracts import ContractModel, StableCode

ScenarioCategory = Literal[
    "SUPPLIED_BRIEF",
    "OPPORTUNITY_DISCOVERY",
    "INTELLIGENCE",
    "INVENTORY_EXTRACTION",
    "PLANNING_OPTIMISATION",
    "PROPOSAL_TRANSACTION",
    "FUNDING_CAMPAIGN_DELIVERY",
    "MEASUREMENT_LEARNING",
]
ExecutionSurface = Literal["AGENT_RUNTIME", "COMMERCIAL_API", "INTEGRATED"]


class ScenarioInvariant(ContractModel):
    code: StableCode
    expected: bool | int | str


class BackendScenarioDefinition(ContractModel):
    scenario_id: Annotated[str, Field(pattern=r"^[A-Z]+-[0-9]{3}$")]
    category: ScenarioCategory
    name: Annotated[str, Field(min_length=1, max_length=120)]
    execution_surface: ExecutionSurface
    source_inputs: dict[str, Any]
    expected_invariants: Annotated[tuple[ScenarioInvariant, ...], Field(min_length=1)]


class BackendScenarioRunRecord(ContractModel):
    scenario_id: Annotated[str, Field(pattern=r"^[A-Z]+-[0-9]{3}$")]
    category: ScenarioCategory
    source_inputs: dict[str, Any]
    invariant_results: dict[str, bool]
    actual_result: dict[str, Any]
    lifecycle_terminal_state: StableCode
    human_review_points: tuple[StableCode, ...]
    # A pre-execution failure has no measured fact count.
    unsupported_fact_count: NonNegativeInt | None
    missing_required_fact_count: NonNegativeInt | None
    provider: Annotated[str, Field(min_length=1, max_length=100)]
    model: Annotated[str, Field(min_length=1, max_length=300)]
    tool_calls: NonNegativeInt
    input_tokens: NonNegativeInt
    output_tokens: NonNegativeInt
    incremental_cost_minor: NonNegativeInt
    cost_currency: Literal["USD"]
    latency_ms: NonNegativeInt
    retries: NonNegativeInt
    commercial_reconciliation: StableCode
    tenant_security_result: StableCode
    passed: bool
    execution_status: Literal["EXECUTED", "EXECUTION_FAILED", "NOT_EXECUTED"] = "EXECUTED"
    failure_reasons: tuple[str, ...] = ()


def _invariants(*codes: str) -> tuple[ScenarioInvariant, ...]:
    return tuple(ScenarioInvariant(code=code, expected=True) for code in codes)


def _definitions(
    prefix: str,
    category: ScenarioCategory,
    execution_surface: ExecutionSurface,
    cases: tuple[tuple[str, dict[str, Any]], ...],
    *invariant_codes: str,
) -> tuple[BackendScenarioDefinition, ...]:
    return tuple(
        BackendScenarioDefinition(
            scenario_id=f"{prefix}-{index:03d}",
            category=category,
            name=name,
            execution_surface=execution_surface,
            source_inputs={"synthetic": True, "case": name, **inputs},
            expected_invariants=_invariants(*invariant_codes),
        )
        for index, (name, inputs) in enumerate(cases, start=1)
    )


BRIEF_CASES = (
    ("simple OOH", {"mode": "OOH_ONLY"}),
    ("complex OOH", {"mode": "OOH_ONLY", "complexity": "HIGH"}),
    ("full campaign", {"mode": "FULL_CAMPAIGN"}),
    ("fixed budget", {"budget_mode": "FIXED"}),
    ("no budget", {"budget_mode": "ABSENT"}),
    ("range budget", {"budget_mode": "RANGE"}),
    ("impossible budget constraint", {"budget_mode": "IMPOSSIBLE"}),
    ("mandatory channels", {"channel_policy": "MANDATORY"}),
    ("prohibited channels", {"channel_policy": "PROHIBITED"}),
    ("mandatory locations", {"location_policy": "MANDATORY"}),
    ("exclusions", {"constraints": "EXCLUSIONS"}),
    ("national", {"geography_scope": "NATIONAL"}),
    ("hyper-local", {"geography_scope": "HYPER_LOCAL"}),
    ("mixed language", {"language_shape": "MIXED"}),
    ("badly formatted email", {"source_shape": "MALFORMED_EMAIL"}),
    ("forwarded chain", {"source_shape": "FORWARDED_CHAIN"}),
    ("attachment plus email", {"source_shape": "EMAIL_ATTACHMENT"}),
    ("contradictory requirements", {"conflict": True}),
    ("missing dates", {"missing": "DATES"}),
    ("missing audience", {"missing": "AUDIENCE"}),
)

OPPORTUNITY_CASES = (
    ("evidence-rich", {"evidence": "RICH"}),
    ("weak evidence", {"evidence": "WEAK"}),
    ("no evidence", {"evidence": "NONE"}),
    ("multiple angles", {"angle_count": "MULTIPLE"}),
    ("duplicate opportunity detection", {"duplicate": True}),
    ("stale signal", {"signal_freshness": "STALE"}),
    ("existing client/campaign context", {"context": "EXISTING"}),
    ("supply change", {"change": "SUPPLY"}),
    ("geography change", {"change": "GEOGRAPHY"}),
    ("non-actionable hypothesis", {"actionability": "NONE"}),
)

INTELLIGENCE_CASES = (
    ("broad audience", {"audience_scope": "BROAD"}),
    ("niche audience", {"audience_scope": "NICHE"}),
    ("cultural/community targeting", {"audience_scope": "COMMUNITY"}),
    ("age/lifestage", {"audience_scope": "AGE_LIFESTAGE"}),
    ("B2B", {"audience_scope": "B2B"}),
    ("POI-dependent", {"location_basis": "POI"}),
    ("corridor/movement", {"location_basis": "CORRIDOR"}),
    ("no reliable geo evidence", {"geo_evidence": "NONE"}),
    ("conflicting evidence", {"evidence_conflict": True}),
    ("evidence age conflict", {"evidence_freshness": "CONFLICT"}),
    ("multiple provinces", {"geography_scope": "MULTI_PROVINCE"}),
    ("township", {"geography_context": "TOWNSHIP"}),
    ("premium urban", {"geography_context": "PREMIUM_URBAN"}),
    ("audience overlap", {"audience_overlap": True}),
    ("unsupported claim rejection", {"forged_claim": True}),
)

INVENTORY_CASES = (
    ("XLSX flat table", {"document": "XLSX", "shape": "FLAT"}),
    ("XLSX merged headers", {"document": "XLSX", "shape": "MERGED_HEADERS"}),
    ("transposed matrix", {"document": "XLSX", "shape": "TRANSPOSED"}),
    ("multi-section worksheet", {"document": "XLSX", "shape": "MULTI_SECTION"}),
    ("package rates", {"commercial_shape": "PACKAGE"}),
    ("discounts", {"commercial_shape": "DISCOUNT"}),
    ("missing rates", {"missing": "RATE"}),
    ("overlapping dates", {"date_shape": "OVERLAPPING"}),
    ("PPTX", {"document": "PPTX"}),
    ("PDF rate card", {"document": "PDF", "shape": "RATE_CARD"}),
    ("PDF brochure/catalogue", {"document": "PDF", "shape": "CATALOGUE"}),
    ("mixed narrative/table", {"document": "PDF", "shape": "MIXED"}),
    ("missing supplier identity", {"missing": "SUPPLIER_IDENTITY"}),
    ("duplicate product rows", {"duplicate_rows": True}),
    ("malformed/ambiguous source", {"source_quality": "AMBIGUOUS"}),
)

PLANNING_CASES = (
    ("one channel", {"channels": 1}),
    ("multi-channel", {"channels": 4}),
    ("insufficient inventory", {"inventory": "INSUFFICIENT"}),
    ("stale inventory", {"inventory": "STALE"}),
    ("unavailable site", {"availability": "UNAVAILABLE"}),
    ("missing cost", {"missing": "COST"}),
    ("multi-geography collective coverage", {"coverage": "COLLECTIVE"}),
    ("creative incompatibility", {"creative_fit": "INCOMPATIBLE"}),
    ("lower-cost alternative", {"alternative": "LOWER_COST"}),
    ("max measured reach where evidence exists", {"objective": "MAX_MEASURED_REACH"}),
    ("reach unknown", {"reach": "UNKNOWN"}),
    ("substitution", {"substitution": True}),
    ("fixed-budget optimisation", {"budget_change": "NONE"}),
    ("budget increase", {"budget_change": "INCREASE"}),
    ("budget decrease", {"budget_change": "DECREASE"}),
)

PROPOSAL_CASES = (
    ("single option", {"option_count": 1}),
    ("three options", {"option_count": 3}),
    ("proposal inventory goes stale", {"inventory_change": "STALE"}),
    ("client selects", {"decision": "SELECTED"}),
    ("client declines", {"decision": "DECLINED"}),
    ("RFQ expires", {"rfq_state": "EXPIRED"}),
    ("supplier response expires", {"response_state": "EXPIRED"}),
    ("counter/accept", {"negotiation": "COUNTER_ACCEPT"}),
    ("supplier changes availability", {"change": "AVAILABILITY"}),
    ("booking confirmation", {"booking": "CONFIRMED"}),
)

FUNDING_CASES = (
    ("EFT", {"funding_route": "EFT"}),
    ("inactive provider rejected", {"provider_state": "INACTIVE"}),
    ("duplicate manual payment reconciliation", {"manual_reconciliation": "DUPLICATE"}),
    ("PO mismatch", {"mismatch": "PURCHASE_ORDER"}),
    ("invoice mismatch", {"mismatch": "INVOICE"}),
    ("booking before funding rejected", {"ordering_violation": "BOOKING_BEFORE_FUNDING"}),
    ("readiness missing", {"readiness": "MISSING"}),
    ("delivery proof accepted", {"proof": "APPROVED"}),
    ("delivery proof rejected", {"proof": "REJECTED"}),
    ("supplier tenant attack", {"security_attack": "SUPPLIER_CROSS_TENANT"}),
)

MEASUREMENT_CASES = (
    ("complete evidence", {"evidence": "COMPLETE"}),
    ("missing proof", {"evidence": "MISSING_PROOF"}),
    ("incompatible methodology", {"methodology": "INCOMPATIBLE"}),
    ("limitations preserved", {"limitations": "REQUIRED"}),
    ("derived commercial benchmark recomputation", {"memory": "RECOMPUTE"}),
)

BACKEND_ACCEPTANCE_SCENARIOS = (
    *_definitions(
        "BRIEF", "SUPPLIED_BRIEF", "AGENT_RUNTIME", BRIEF_CASES,
        "SOURCE_FACTS_RETAINED", "NO_UNSUPPORTED_FACTS", "MODE_DOES_NOT_SILENTLY_EXPAND"),
    *_definitions(
        "OPP", "OPPORTUNITY_DISCOVERY", "COMMERCIAL_API", OPPORTUNITY_CASES,
        "EVIDENCE_REQUIRED_FOR_ASSERTION", "SUPPLIED_BRIEF_REMAINS_DISTINCT", "NO_FAKE_CLIENT_REQUEST"),
    *_definitions(
        "INTEL", "INTELLIGENCE", "AGENT_RUNTIME", INTELLIGENCE_CASES,
        "EVIDENCE_BINDINGS_VALID", "UNKNOWN_REMAINS_UNKNOWN", "NO_UNSUPPORTED_FACTS"),
    *_definitions(
        "INV", "INVENTORY_EXTRACTION", "INTEGRATED", INVENTORY_CASES,
        "SOURCE_ACCOUNTING_COMPLETE", "NO_SUPPLIER_FILE_HARDCODING", "AUTO_PUBLISH_DISABLED"),
    *_definitions(
        "PLAN", "PLANNING_OPTIMISATION", "COMMERCIAL_API", PLANNING_CASES,
        "CANONICAL_ELIGIBILITY_USED", "CANONICAL_CLIENT_PRICE_USED", "NO_ASSUMED_REACH"),
    *_definitions(
        "TXN", "PROPOSAL_TRANSACTION", "COMMERCIAL_API", PROPOSAL_CASES,
        "HISTORICAL_VERSION_IMMUTABLE", "TRANSACTION_ORDER_ENFORCED", "NEGOTIATION_LINEAGE_RETAINED"),
    *_definitions(
        "FUND", "FUNDING_CAMPAIGN_DELIVERY", "COMMERCIAL_API", FUNDING_CASES,
        "FUNDING_BEFORE_BOOKING", "CONSEQUENCE_AUTHORITY_ENFORCED", "TENANT_SECURITY_PASS"),
    *_definitions(
        "MEASURE", "MEASUREMENT_LEARNING", "COMMERCIAL_API", MEASUREMENT_CASES,
        "APPROVED_EVIDENCE_ONLY", "LIMITATIONS_PRESERVED", "COMMERCIAL_MEMORY_DERIVED"),
)

CATEGORY_COUNTS = {
    "SUPPLIED_BRIEF": 20,
    "OPPORTUNITY_DISCOVERY": 10,
    "INTELLIGENCE": 15,
    "INVENTORY_EXTRACTION": 15,
    "PLANNING_OPTIMISATION": 15,
    "PROPOSAL_TRANSACTION": 10,
    "FUNDING_CAMPAIGN_DELIVERY": 10,
    "MEASUREMENT_LEARNING": 5,
}
