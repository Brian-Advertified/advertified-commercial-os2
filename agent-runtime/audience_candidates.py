"""Supplied audience candidates and explicit gaps for research review."""

from contracts import UnknownItem
from planning_contracts import AudienceAgentRequest


DISCOVERY_INSTRUCTION = (
    "Evaluate supplied audiences and propose distinct candidate segments only where "
    "the approved Brief supports a commercial rationale; do not pad to a fixed count. "
    "Recommend primary and secondary targets, leave unsuitable or "
    "weakly supported candidates untargeted, and explain needs, buying contexts, "
    "exclusions and positioning. Treat derived segments as evidence-labelled "
    "inferences or hypotheses, never as verified demographic facts. "
    "Distinguish the campaign objective from a consumer need. Identify missing "
    "product, price, purchase occasion and decision-making context explicitly. "
    "Explain what audience research, media-use, daypart and location evidence would "
    "validate each hypothesis; do not claim it was researched when it was not supplied. "
    "Never infer affluence, language, travel routines or media habits from a persona label."
)


def candidate_audience_names(request: AudienceAgentRequest) -> list[str]:
    return list(dict.fromkeys(request.planning.audiences))


def audience_research_unknowns(existing=()) -> tuple[UnknownItem, ...]:
    questions = {
        "artifact.audiences.buying_context": (
            "What product, price, purchase occasion and decision-making role support "
            "each audience, and which are supplied facts versus hypotheses?"
        ),
        "artifact.audiences.media_evidence": (
            "Which dated aggregate audience studies establish target size, media use, "
            "dayparts, travel or shopping locations, reach and frequency in the target area?"
        ),
        "artifact.audiences.structured_evidence": (
            "Which approved evidence supports language, life-stage and segmentation "
            "labels, including the exact taxonomy and version? These inputs are not supplied."
        ),
    }
    existing_paths = {item.field_path for item in existing}
    return tuple(existing) + tuple(
        UnknownItem(field_path=path, question=question, is_blocking=False)
        for path, question in questions.items() if path not in existing_paths
    )
