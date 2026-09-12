"""Provider channel choices use the supplied scope and existing presentation labels."""
import json

from media_presentation import CHANNEL_LABELS
from media_strategy_contracts import MediaStrategyArtifact, MediaStrategyRequest


def media_strategy_schema(request: MediaStrategyRequest) -> str:
    schema = MediaStrategyArtifact.model_json_schema()
    context = request.media_strategy
    channels = list(context.available_channels)
    labels = "; ".join(f"{code}: {CHANNEL_LABELS.get(code, code)}" for code in channels)
    fields = schema["$defs"]["MediaChannelRecommendation"]["properties"]
    fields["channel"]["enum"] = channels
    fields["channel"]["description"] = (
        f"Exact registry code. {labels}. Match the requested medium to its specific code, "
        "not an umbrella term. Explicit client restrictions narrow the permitted recommendations."
    )
    fields["role"]["description"] = (
        "Proposed communication task for this channel, tied to the approved objective. "
        "State what message it could carry, not where an audience is found or how effectively "
        "it will reach them. No traffic, footfall, audience presence, affinity or engagement "
        "evidence is supplied. Requested proximity to a POI does not establish any audience trait."
    )
    schema["properties"]["excluded_channels"]["items"]["enum"] = channels
    if context.budget_minor is None:
        fields["budget_guidance_percent"] = {"type": "null"}
    return json.dumps(schema, separators=(",", ":"))
