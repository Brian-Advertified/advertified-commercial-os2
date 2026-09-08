"""Report only the typed-contract error shape from the latest rejected Bedrock result."""
from __future__ import annotations

import json
import sys

from pydantic import ValidationError

from inventory_ai_cost_ledger import REPO_ROOT, query_json

sys.path.insert(0, str(REPO_ROOT / "agent-runtime"))

from contracts import GeneratedAgentOutput  # noqa: E402
from supplied_brief_contracts import SuppliedBriefArtifact  # noqa: E402
from supplied_brief_service import _canonical_excerpt  # noqa: E402


def main() -> int:
    row = query_json("""
        SELECT jsonb_build_object(
            'output', step.output_json,
            'sourceTitle', brief.source_title,
            'sourceContent', brief.source_content,
            'clarifications', brief.clarifications_json
        )
        FROM commercial.agent_run_steps AS step
        JOIN commercial.supplied_brief_interpretations AS brief
          ON brief.tenant_id = step.tenant_id
         AND brief.id = step.run_id
        WHERE step.output_json ? 'RejectedResponse'
           OR step.output_json ? 'rejectedResponse'
        ORDER BY step.updated_at_utc DESC
        LIMIT 1
    """)
    if not isinstance(row, dict) or not isinstance(row.get("output"), dict):
        raise RuntimeError("No retained rejected Bedrock response was found.")
    stored = row["output"]
    rejected_response = (
        stored.get("RejectedResponse")
        or stored.get("rejectedResponse")
    )
    response = json.loads(str(rejected_response))
    detail = response.get("detail") if isinstance(response, dict) else None
    payload = detail.get("rejected_output") if isinstance(detail, dict) else None
    if payload is None:
        result = response if isinstance(response, dict) else {}
        questions = result.get("questions") or result.get("Questions") or []
        evidence = result.get("evidence") or result.get("Evidence") or []
        print(json.dumps({
            "stage": "API_RESULT_VALIDATION",
            "validationError": (
                stored.get("ValidationError")
                or stored.get("validationError")
            ),
            "campaignMode": (
                result.get("campaignMode")
                or result.get("CampaignMode")
            ),
            "requiresHumanClarification": (
                result.get("requiresHumanClarification")
                if "requiresHumanClarification" in result
                else result.get("RequiresHumanClarification")
            ),
            "questionFields": [
                item.get("fieldPath") or item.get("FieldPath")
                for item in questions
                if isinstance(item, dict)
            ],
            "evidenceFields": [
                item.get("fieldPath") or item.get("FieldPath")
                for item in evidence
                if isinstance(item, dict)
            ],
        }, indent=2))
        return 0
    try:
        GeneratedAgentOutput[SuppliedBriefArtifact].model_validate_json(
            json.dumps(payload)
        )
    except ValidationError as error:
        print(json.dumps({
            "stage": detail.get("stage"),
            "providerAcceptance": detail.get("provider_acceptance"),
            "errors": [
                {
                    "location": [str(part) for part in item["loc"]],
                    "type": item["type"],
                    "message": item["msg"],
                }
                for item in error.errors(
                    include_input=False,
                    include_context=False,
                )
            ],
        }, indent=2))
        return 0
    clarifications = row.get("clarifications")
    sources = {
        "supplied:title": str(row.get("sourceTitle") or ""),
        "supplied:brief/current": str(row.get("sourceContent") or ""),
        **{
            f"clarification:{item.get('fieldPath')}": str(item.get("value") or "")
            for item in (clarifications if isinstance(clarifications, list) else [])
            if isinstance(item, dict)
        },
    }
    artifact = payload.get("artifact") if isinstance(payload, dict) else None
    evidence = artifact.get("evidence") if isinstance(artifact, dict) else None
    grounding_errors = []
    grounded_fields = []
    for index, item in enumerate(evidence if isinstance(evidence, list) else []):
        if not isinstance(item, dict):
            continue
        locator = str(item.get("source_locator") or "")
        excerpt = str(item.get("excerpt") or "")
        declared = sources.get(locator)
        if declared is not None and excerpt and excerpt in declared:
            grounded_fields.append(item.get("field_path"))
            continue
        grounding_errors.append({
            "index": index,
            "fieldPath": item.get("field_path"),
            "declaredLocator": locator,
            "exactMatchingLocators": [
                key for key, source in sources.items()
                if excerpt and excerpt in source
            ],
            "canonicalMatchingLocators": [
                key for key, source in sources.items()
                if _canonical_excerpt(source, excerpt) is not None
            ],
        })
    print(json.dumps({
        "stage": detail.get("stage"),
        "providerAcceptance": detail.get("provider_acceptance"),
        "errors": [],
        "groundedFields": grounded_fields,
        "groundingErrors": grounding_errors,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
