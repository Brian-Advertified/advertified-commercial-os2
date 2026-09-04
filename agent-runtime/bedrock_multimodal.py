"""Build bounded Bedrock multimodal content without persisting source binaries."""

from __future__ import annotations

import base64
import json
import math

from botocore.exceptions import ClientError
from pydantic import BaseModel

CHARACTERS_PER_TOKEN_RESERVE = 3
FIXED_INPUT_TOKEN_RESERVE = 32_768
IMAGE_INPUT_TOKEN_RESERVE = 4_096


class BedrockMultimodalError(ValueError):
    """Raised when multimodal input cannot be represented safely."""


def request_content(
    request: BaseModel,
    model: str,
    multimodal_models: frozenset[str],
) -> list[dict[str, object]]:
    payload = request.model_dump(mode="json")
    payload.pop("source_images", None)
    images = tuple(getattr(request, "source_images", ()))
    if images and model not in multimodal_models:
        raise BedrockMultimodalError("The requested Bedrock model is not approved for images.")
    if images:
        payload["source_images"] = [
            {
                "ordinal": image.ordinal,
                "locator": image.locator,
                "format": image.format,
                "sha256": image.sha256,
                "byte_length": image.byte_length,
            }
            for image in images
        ]
    content: list[dict[str, object]] = [
        {
            "text": json.dumps(
                payload,
                separators=(",", ":"),
                sort_keys=True,
            ),
        }
    ]
    for image in images:
        content.append(
            {
                "text": (
                    "The next attached image has ordinal "
                    f"{image.ordinal} and exact source locator "
                    f"{image.locator}. Cite that complete locator verbatim; "
                    "do not append cells, rows, coordinates, or other suffixes."
                ),
            }
        )
        content.append(
            {
                "image": {
                    "format": image.format,
                    "source": {
                        "bytes": base64.b64decode(
                            image.data_base64,
                            validate=True,
                        ),
                    },
                },
            }
        )
    return content


def count_input_tokens(
    client,
    model: str,
    system: list[dict[str, str]],
    messages: list[dict[str, object]],
) -> int | None:
    try:
        response = client.count_tokens(
            modelId=model,
            input={
                "converse": {
                    "system": system,
                    "messages": messages,
                },
            },
        )
    except ClientError as error:
        details = error.response.get("Error", {})
        if details.get(
            "Code"
        ) == "ValidationException" and "doesn't support counting tokens" in str(
            details.get("Message", "")
        ):
            return None
        raise
    try:
        value = int(response["inputTokens"])
    except (KeyError, TypeError, ValueError) as error:
        raise BedrockMultimodalError("Bedrock token count is incomplete.") from error
    if value <= 0:
        raise BedrockMultimodalError("Bedrock token count is invalid.")
    return value


def conservative_input_token_estimate(
    system: list[dict[str, str]],
    messages: list[dict[str, object]],
) -> int:
    text_characters = sum(len(block.get("text", "")) for block in system)
    image_count = 0
    for message in messages:
        for block in message.get("content", []):  # type: ignore[union-attr]
            if "text" in block:
                text_characters += len(block["text"])
            elif "image" in block:
                image_count += 1
    return (
        math.ceil(text_characters / CHARACTERS_PER_TOKEN_RESERVE)
        + FIXED_INPUT_TOKEN_RESERVE
        + image_count * IMAGE_INPUT_TOKEN_RESERVE
    )
