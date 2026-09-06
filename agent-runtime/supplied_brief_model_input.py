"""Build the minimal model-facing input for supplied-Brief understanding."""

from __future__ import annotations

import re
from typing import Any

from supplied_brief_contracts import SuppliedBriefRequest

PRIMARY_LOCATOR = "supplied:brief/current"
HISTORY_LOCATOR = "supplied:brief/history"
_QUOTED_HISTORY = re.compile(
    r"(?im)^(?:-{2,}\s*original message\s*-{2,}\s*$|"
    r"on .{1,300} wrote:\s*$|"
    r"from:\s*[^\n]+\n(?:sent|date):\s*[^\n]+\n"
    r"to:\s*[^\n]+(?:\nsubject:\s*[^\n]+)?)"
)


def source_segments(request: SuppliedBriefRequest) -> tuple[dict[str, str], ...]:
    """Partition only when a stable quoted-message boundary is present."""
    content = request.source.source_content
    boundary = _QUOTED_HISTORY.search(content)
    if boundary is None or not content[: boundary.start()].strip():
        return ({
            "segment_id": PRIMARY_LOCATOR,
            "role": "PRIMARY_MESSAGE",
            "content": content,
        },)
    return (
        {
            "segment_id": PRIMARY_LOCATOR,
            "role": "PRIMARY_MESSAGE",
            "content": content[: boundary.start()],
        },
        {
            "segment_id": HISTORY_LOCATOR,
            "role": "QUOTED_HISTORY",
            "content": content[boundary.start():],
        },
    )


def model_source_locators(request: SuppliedBriefRequest) -> tuple[str, ...]:
    """Return only locators that are present in this exact model input."""
    return (
        "supplied:title",
        *(segment["segment_id"] for segment in source_segments(request)),
        *(f"clarification:{item.field_path}" for item in request.source.clarifications),
    )


def build_model_input(request: SuppliedBriefRequest) -> dict[str, Any]:
    """Exclude invocation, identity, provider, cost and checkpoint metadata."""
    return {
        "operation": request.operation,
        "source_revision": {"source_hash": request.source.source_hash},
        "source": {
            "title": {
                "source_locator": "supplied:title",
                "content": request.source.source_title,
            },
            "segments": source_segments(request),
        },
        "clarifications": tuple(
            {
                "field_path": item.field_path,
                "source_locator": f"clarification:{item.field_path}",
                "content": item.value,
            }
            for item in request.source.clarifications
        ),
    }
