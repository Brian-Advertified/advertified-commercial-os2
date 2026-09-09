"""The inventory agent accepts canonical planner reasoning without inventing evidence."""

import json

from buy_assessment_contracts import InventoryBuyAssessmentFacts


def test_planner_context_and_unresolved_buying_warning_survive_contract_validation():
    facts = InventoryBuyAssessmentFacts.model_validate_json(json.dumps({
        "is_target_audience": False,
        "evidence_gaps": ["buyAssessment.targetUniverse"],
        "planner_reasoning": {
            "planned_channel_role": "Remind shoppers near the store",
            "target_contexts": [{
                "name": "Parents", "need_state": "Find school supplies",
                "buying_context": "Compare nearby stores",
            }],
            "required_places_matched": 1,
            "required_places_total": 1,
            "has_measured_target_audience": False,
            "review_questions": ["plannerReasoning.proximityNotAudience"],
            "supported_reasons": ["plannerReasoning.requiredPlacesCovered"],
            "buying_warnings": ["plannerReasoning.creativeDoesNotFit"],
        },
    }))
    assert facts.reach is None
    assert not facts.planner_reasoning.has_measured_target_audience
    assert facts.planner_reasoning.target_contexts[0].buying_context == "Compare nearby stores"
    assert facts.planner_reasoning.buying_warnings == ("plannerReasoning.creativeDoesNotFit",)
