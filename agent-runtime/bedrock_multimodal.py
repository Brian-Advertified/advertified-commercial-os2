"""Build bounded Bedrock multimodal content without persisting source binaries."""

from __future__ import annotations

import base64
import json
from typing import Any

from botocore.exceptions import ClientError
from pydantic import BaseModel

FIXED_INPUT_TOKEN_RESERVE = 8_192
IMAGE_INPUT_TOKEN_RESERVE = 4_096


class BedrockMultimodalError(ValueError):
    """Raised when multimodal input cannot be represented safely."""


def request_content(
    request: BaseModel | dict[str, Any],
    model: str,
    multimodal_models: frozenset[str],
    *,
    source_images: tuple[Any, ...] | None = None,
) -> list[dict[str, object]]:
    payload, images = _payload_and_images(request, source_images)
    payload.pop("source_images", None)
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


def _payload_and_images(
    request: BaseModel | dict[str, Any],
    source_images: tuple[Any, ...] | None,
) -> tuple[dict[str, Any], tuple[Any, ...]]:
    if isinstance(request, BaseModel):
        payload = request.model_dump(mode="json")
        embedded_images = tuple(getattr(request, "source_images", ()))
    else:
        payload = dict(request)
        embedded_images = ()
    images = embedded_images if source_images is None else source_images
    return payload, images


def count_input_tokens(
    client,
    model: str,
    system: list[dict[str, str]],
    messages: list[dict[str, object]],
    tool_config: dict[str, object] | None = None,
) -> int | None:
    try:
        response = client.count_tokens(
            modelId=model,
            input={
                "converse": {
                    "system": system,
                    "messages": messages,
                    **({"toolConfig": tool_config} if tool_config is not None else {}),
                },
            },
        )
    except ClientError as error:
        details = error.response.get("Error", {})
        code = details.get("Code")
        if code == "ValidationException" and "doesn't support counting tokens" in str(
            details.get("Message", "")
        ):
            return None
        # CountTokens is an optional preflight capability. Some governed roles are allowed
        # to invoke Bedrock models but are not granted bedrock:CountTokens. In that case
        # fall back to the deliberately conservative local byte-based estimate below rather
        # than weakening the per-call cost cap or blocking a provider invocation entirely.
        if code == "AccessDeniedException":
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
    tool_config: dict[str, object] | None = None,
) -> int:
    # Reserve one token per UTF-8 byte rather than an average character/token ratio.
    # The schema occurs in both the system prompt and the actual tool configuration.
    text_bytes = sum(len(block.get("text", "").encode("utf-8")) for block in system)
    if tool_config is not None:
        text_bytes += len(json.dumps(tool_config, ensure_ascii=True).encode("utf-8"))
    image_count = 0
    for message in messages:
        for block in message.get("content", []):  # type: ignore[union-attr]
            if "text" in block:
                text_bytes += len(block["text"].encode("utf-8"))
            elif "image" in block:
                image_count += 1
    return (
        text_bytes
        + FIXED_INPUT_TOKEN_RESERVE
        + image_count * IMAGE_INPUT_TOKEN_RESERVE
    )
