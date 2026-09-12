"""Explicit synthetic model proposals for the catalogue's supplied-Brief cases."""
import json
from uuid import UUID, uuid5

from contracts import AgentOutputEnvelope, ProviderUsage
from bedrock_supplied_brief_output import wrap_supplied_brief_output
from supplied_brief_contracts import SuppliedBriefArtifact
from tests.test_supplied_brief_model_input import supplied_request

NAMESPACE = UUID("fb594e81-6c8b-4f73-a3c9-ea6beded0c97")


def fixture(scenario):
    spec = case_values(scenario)
    content = source_content(scenario, spec)
    request = supplied_request(content)
    invocation = request.invocation.model_copy(update={
        "run_id": uuid5(NAMESPACE, scenario.scenario_id),
        "step_id": uuid5(NAMESPACE, scenario.scenario_id + ":brief"),
    })
    request = request.model_copy(update={
        "invocation": invocation,
        "source": request.source.model_copy(update={
            "source_title": scenario.name, "clarifications": (),
        }),
    })
    proposal = output_proposal(request, spec)
    return request, proposal, spec


def case_values(scenario):
    inputs = scenario.source_inputs
    spec = dict(
        business_problem="Low awareness of a synthetic service",
        objective="Increase qualified enquiries",
        audiences=["Adults who request the service"],
        geographies=["Gauteng"], timing="October 2026",
        mode="OOH_ONLY", media=["Media: OOH and DOOH only."],
        budget_minor=1847335, currency="ZAR", budget_text="Budget: ZAR 18,473.35",
        constraints=[], conflicts=[],
    )
    apply_brief_scope(spec, inputs)
    apply_brief_budget(spec, inputs)
    if inputs.get("missing") == "DATES":
        spec["timing"] = ""
    if inputs.get("missing") == "AUDIENCE":
        spec["audiences"] = []
    return spec


def apply_brief_scope(spec, inputs):
    if inputs.get("mode") == "FULL_CAMPAIGN":
        spec.update(mode="FULL_CAMPAIGN", media=["Media: full campaign including email and OOH."])
    if inputs.get("complexity") == "HIGH":
        spec.update(objective="Mukuru synthetic brief: support service discovery",
                    audiences=["Zimbabwean and Malawian service users"],
                    geographies=["Gauteng", "Limpopo"],
                    constraints=["Formats: billboards and wall murals only."])
    if inputs.get("channel_policy") == "MANDATORY":
        spec.update(objective="Takealot synthetic digital OOH launch",
                    media=["Media: DOOH only. Digital screens are mandatory."])
    if inputs.get("channel_policy") == "PROHIBITED":
        spec["constraints"] = ["Exclusions: do not use social media."]
    if inputs.get("constraints") == "EXCLUSIONS":
        spec["constraints"] = ["Exclusions: do not use 3 x 6 panels."]
    if inputs.get("location_policy") == "MANDATORY":
        spec["constraints"] = ["Requirements: only Pretoria and Polokwane."]
        spec["geographies"] = ["Pretoria", "Polokwane"]
    if inputs.get("geography_scope") == "NATIONAL":
        spec["geographies"] = ["South Africa"]
    if inputs.get("geography_scope") == "HYPER_LOCAL":
        spec["geographies"] = ["Within 2 km of the supplied clinic"]
    if inputs.get("language_shape") == "MIXED":
        spec["objective"] = "Increase enquiries / Khulisa ulwazi"
    if inputs.get("conflict"):
        spec.update(mode=None, media=["Media: OOH only.", "Media: full campaign including email."],
                    conflicts=["OOH-only scope conflicts with required email."])


def apply_brief_budget(spec, inputs):
    mode = inputs.get("budget_mode")
    if mode == "ABSENT" or inputs.get("source_shape") == "FORWARDED_CHAIN":
        spec.update(budget_minor=None, currency=None, budget_text="")
    if mode == "RANGE":
        spec.update(budget_minor=None, currency=None,
                    budget_text="Budget: ZAR 10,000 to ZAR 20,000; final amount undecided.")
    if mode == "IMPOSSIBLE":
        spec.update(budget_minor=100, budget_text="Budget: ZAR 1.00")
        spec["constraints"] = ["Requirements: deliver 100 panels within ZAR 1.00; feasibility unverified."]
    if inputs.get("source_shape") == "FORWARDED_CHAIN":
        spec.update(mode="FULL_CAMPAIGN", media=["Media: full campaign including email."])


def source_content(scenario, spec):
    lines = [
        "Problem: " + spec["business_problem"], "Objective: " + spec["objective"],
        *("Audience: " + item for item in spec["audiences"]),
        *("Geography: " + item for item in spec["geographies"]),
        *spec["media"], *spec["constraints"],
    ]
    if spec["timing"]:
        lines.append("Timing: " + spec["timing"])
    if spec["budget_text"]:
        lines.append(spec["budget_text"])
    shape = scenario.source_inputs.get("source_shape")
    if shape == "EMAIL_ATTACHMENT":
        lines.append("Attachment mentioned: media-details.xlsx (contents not supplied).")
    text = "\n".join(lines)
    if shape == "MALFORMED_EMAIL":
        text = "From??? synthetic\n<unclosed>\n" + text
    if shape == "FORWARDED_CHAIN":
        text += "\n\nOn July 12, 2026, Previous Sender wrote:\nMedia: OOH only.\nBudget: ZAR 90,000"
    return text


def output_proposal(request, spec):
    absent = [field for field, value in (
        ("timing", spec["timing"]), ("audiences", spec["audiences"]),
    ) if not value]
    questions = [dict(field_path=field, question="Please supply " + field,
                      is_blocking=True, options=[]) for field in absent]
    if spec["conflicts"]:
        questions.append(dict(field_path="campaignMode", question="Resolve campaign scope.",
                              is_blocking=True, options=[]))
    draft = dict(
        business_problem=spec["business_problem"], objective=spec["objective"],
        audiences=spec["audiences"], geographies=spec["geographies"], timing=spec["timing"],
        budget_minor=spec["budget_minor"], budget_unknown=spec["budget_minor"] is None,
        currency=spec["currency"], vat_status=None, fees_minor=None,
        media_requirements=spec["media"], constraints=spec["constraints"],
        measurement=[], facts=[], assumptions=[],
        unknowns=[dict(field_path=field, question="Please supply " + field, is_blocking=True)
                  for field in absent],
        conflicts=[dict(field_path="campaignMode", description=item, severity="BLOCKING",
                        resolved=False, resolution=None) for item in spec["conflicts"]],
    )
    payload = dict(
        source_hash=request.source.source_hash, client_name=None, title=request.source.source_title,
        campaign_mode=spec["mode"], campaign_mode_confidence=0 if spec["mode"] is None else 1,
        requires_human_clarification=bool(questions), campaign_mode_rationale="Synthetic source proposal.",
        draft=draft, questions=questions, evidence=evidence_for(request, spec),
    )
    generated = wrap_supplied_brief_output(SuppliedBriefArtifact, payload)
    envelope = generated.model_dump(mode="json")
    envelope["usage"] = ProviderUsage(
        provider="deterministic", model="fixture-v1", units=0, tool_calls=0,
        incremental_cost_minor=0, cache_status="FIXTURE",
    ).model_dump(mode="json")
    return AgentOutputEnvelope[SuppliedBriefArtifact].model_validate_json(json.dumps(envelope))


def evidence_for(request, spec):
    pairs = [("businessProblem", "Problem: " + spec["business_problem"]),
             ("objective", "Objective: " + spec["objective"])]
    pairs += [("mediaRequirements", line) for line in spec["media"]]
    pairs += [("audiences", "Audience: " + value) for value in spec["audiences"]]
    pairs += [("geographies", "Geography: " + value) for value in spec["geographies"]]
    if spec["timing"]:
        pairs.append(("timing", "Timing: " + spec["timing"]))
    if spec["budget_text"]:
        pairs.append(("budget", spec["budget_text"]))
    return [dict(field_path=field, kind="SUPPLIED_CLAIM", excerpt=excerpt, confidence=1,
                 source_locator="supplied:brief/current") for field, excerpt in pairs]
