"""Governed per-agent Bedrock instructions and opportunity artifact map.

Instructions carry no business or security policy: they bound what the model
may propose. Ownership, validation and acceptance stay in deterministic code
(runtime_execution and the per-service grounding validators).
"""

from __future__ import annotations

from pydantic import BaseModel

from agent_registry import AgentCode
from audience_candidates import DISCOVERY_INSTRUCTION
from inventory_strategy import INTERPRETATION_INSTRUCTION
from market_intelligence_service import INSTRUCTION as MARKET_INTELLIGENCE_INSTRUCTION
from media_presentation import CLIENT_WORDING_INSTRUCTION
from opportunity_contracts import (
    BriefDraftArtifact,
    BusinessInterpretationArtifact,
    CriticReportArtifact,
    OpportunityAngleSetArtifact,
    StrategyArtifact,
)

ArtifactType = type[BaseModel]

OPPORTUNITY_ARTIFACTS: dict[AgentCode, ArtifactType] = {
    AgentCode.BUSINESS_INTERPRETATION: BusinessInterpretationArtifact,
    AgentCode.OPPORTUNITY_INTELLIGENCE: OpportunityAngleSetArtifact,
    AgentCode.STRATEGY: StrategyArtifact,
    AgentCode.CRITIC_READINESS: CriticReportArtifact,
    AgentCode.BRIEF_DRAFTING: BriefDraftArtifact,
}

ENRICHMENT_INSTRUCTION = (
    "Enrich only the deterministic inventory rows supplied in existing_rows. "
    "Do not create inventory and do not repeat, replace or reinterpret supplier, "
    "price, currency, buying basis, validity, availability, geography, format, "
    "placement, dimensions or other commercial facts. Each candidate "
    "source_locator must exactly equal the target existing-row locator. You may "
    "return only channel, product_type and description fields. Mark every field "
    "DERIVED_POLICY. For channel and product_type, normalized_value must be one "
    "exact code from governed_codes. For description, raw_value must be a "
    "verbatim source excerpt and normalized_value may be concise searchable copy "
    "that does not add facts. YouTube inventory classifies as SOCIAL and "
    "SOCIAL_PLACEMENT; Google Search and Display inventory classify as DIGITAL "
    "and DIGITAL_PLACEMENT when the source supports it. Never infer FLAT_RATE "
    "or any rate_type. "
    "Never add dates. Omit a field when evidence is insufficient. Account for "
    "every attached image by citing it or listing its locator in "
    "omitted_source_locators. Return only the requested artifact; deterministic "
    "code owns status, governance and acceptance."
)

TRANSCRIPTION_INSTRUCTION = (
    "Transcribe only supplier facts visible in the supplied source items and images. "
    "Preserve raw wording, numbers and source locators exactly. Do not infer semantic "
    "classification, rate type, currency, availability or dates. Return no candidate "
    "without a name or product code, and account for every supplied image by citing "
    "its exact locator or listing it in omitted_source_locators. Deterministic code "
    "owns normalization, acceptance and publication."
)

INSTRUCTIONS: dict[AgentCode, str] = {
    AgentCode.BUSINESS_INTERPRETATION: (
        "Interpret the business from approved evidence only."
    ),
    AgentCode.OPPORTUNITY_INTELLIGENCE: (
        "Propose ranked commercial opportunity angles grounded in approved "
        "evidence."
    ),
    AgentCode.STRATEGY: (
        "Propose a growth strategy from approved evidence and selected prior "
        "artefacts."
    ),
    AgentCode.CRITIC_READINESS: (
        "Critique the proposed strategy and identify material evidence gaps."
    ),
    AgentCode.BRIEF_DRAFTING: (
        "Draft the canonical campaign brief proposal without inventing missing "
        "facts."
    ),
    AgentCode.MARKET_INTELLIGENCE: MARKET_INTELLIGENCE_INSTRUCTION,
    AgentCode.AUDIENCE_INTELLIGENCE: DISCOVERY_INSTRUCTION,
    AgentCode.INVENTORY_INTELLIGENCE: INTERPRETATION_INSTRUCTION,
    AgentCode.PROPOSAL_NARRATIVE: (
        "Draft proposal wording that preserves every supplied commercial fact "
        "exactly. " + CLIENT_WORDING_INSTRUCTION
    ),
    AgentCode.CREATIVE: (
        "Propose creative territories using only rights-cleared assets and "
        "verified facts."
    ),
    AgentCode.MEASUREMENT: (
        "Interpret reviewed performance evidence without unsupported causality."
    ),
}
