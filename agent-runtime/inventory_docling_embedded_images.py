"""Generic second-pass Docling extraction for embedded document images."""

from __future__ import annotations

import base64
import binascii
import copy
import json
import os
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from collections.abc import Callable
from dataclasses import dataclass
from typing import Any

from inventory_docling_structure import page, unwrap_document


ImageConverter = Callable[[str, str, bytes], dict[str, Any]]


@dataclass(frozen=True)
class EmbeddedImageExpansion:
    document: dict[str, Any]
    warnings: tuple[str, ...]


@dataclass(frozen=True)
class PictureExpansion:
    texts: tuple[dict[str, Any], ...]
    tables: tuple[dict[str, Any], ...]
    retained_picture: dict[str, Any] | None
    warnings: tuple[str, ...]
    interpreted: bool


class EmbeddedImageConversionError(RuntimeError):
    """Raised when the configured local Docling conversion cannot complete."""


def expand_embedded_images(
    document: dict[str, Any],
    converter: ImageConverter | None = None,
) -> EmbeddedImageExpansion:
    """OCR image-only Docling wrappers without using filename or supplier rules."""
    pictures = document.get("pictures", [])
    if not _needs_picture_expansion(document, pictures):
        return EmbeddedImageExpansion(document, ())

    expanded = copy.deepcopy(document)
    texts = copy.deepcopy(document.get("texts", []))
    tables = copy.deepcopy(document.get("tables", []))
    remaining: list[dict[str, Any]] = []
    warnings: list[str] = []
    interpreted = 0
    maximum = _maximum_pictures()
    convert = converter or convert_image_with_docling

    for number, picture in enumerate(pictures, start=1):
        if number > maximum:
            remaining.append(copy.deepcopy(picture))
            continue
        result = _expand_picture(document, picture, number, convert)
        texts.extend(result.texts)
        tables.extend(result.tables)
        warnings.extend(result.warnings)
        interpreted += int(result.interpreted)
        if result.retained_picture is not None:
            remaining.append(result.retained_picture)

    if interpreted:
        warnings.insert(
            0,
            f"{interpreted} embedded picture blocks were interpreted through Docling OCR.",
        )
    if len(pictures) > maximum:
        warnings.append(
            f"{len(pictures) - maximum} embedded picture blocks exceeded the "
            f"configured limit of {maximum}."
        )
    expanded["texts"] = texts
    expanded["tables"] = tables
    expanded["pictures"] = remaining
    return EmbeddedImageExpansion(expanded, tuple(warnings))


def _needs_picture_expansion(
    document: dict[str, Any],
    pictures: Any,
) -> bool:
    if not isinstance(pictures, list) or not pictures:
        return False
    if document.get("tables"):
        return False
    texts = document.get("texts", [])
    return not texts or len(texts) < len(pictures)


def _expand_picture(
    document: dict[str, Any],
    picture: Any,
    number: int,
    convert: ImageConverter,
) -> PictureExpansion:
    decoded = _decode_picture(picture)
    if decoded is None:
        return PictureExpansion(
            (), (), copy.deepcopy(picture),
            (f"Embedded picture {number} has no retained data image.",), False,
        )
    media_type, content = decoded
    try:
        child = unwrap_document(convert(
            f"embedded-picture-{number}{_extension(media_type)}",
            media_type,
            content,
        ))
    except (EmbeddedImageConversionError, TypeError, ValueError, KeyError) as error:
        warning = (
            f"Embedded picture {number} could not be interpreted: "
            f"{type(error).__name__}."
        )
        return PictureExpansion(
            (), (), copy.deepcopy(picture), (warning,), False,
        )

    prefix = _picture_locator(document, picture, number)
    texts = tuple(_with_lineage(child.get("texts", []), prefix, number))
    tables = tuple(_with_lineage(
        child.get("tables", []), prefix, number, structure_kind="table",
    ))
    warnings = [
        f"Embedded picture {number} produced "
        f"{len(texts)} text blocks and {len(tables)} tables."
    ]
    nested = len(child.get("pictures", []))
    if nested:
        warnings.append(
            f"Embedded picture {number} retained {nested} nested "
            "picture blocks that were not recursively interpreted."
        )
    return PictureExpansion(texts, tables, None, tuple(warnings), True)


def convert_image_with_docling(
    filename: str,
    media_type: str,
    content: bytes,
) -> dict[str, Any]:
    base_url = os.environ.get("DOCLING_BASE_URL", "").rstrip("/")
    api_key = os.environ.get("DOCLING_API_KEY", "")
    if not base_url or not api_key:
        raise EmbeddedImageConversionError(
            "Embedded-image Docling endpoint is not configured."
        )
    body, content_type = _multipart(filename, media_type, content)
    submission = _request(
        base_url,
        api_key,
        "POST",
        "/v1/convert/file/async",
        body,
        content_type,
    )
    task_id = submission.get("task_id")
    if not isinstance(task_id, str) or not task_id:
        raise EmbeddedImageConversionError("Docling did not return a task id.")

    deadline = time.monotonic() + _timeout_seconds()
    while time.monotonic() < deadline:
        poll = _request(
            base_url,
            api_key,
            "GET",
            f"/v1/status/poll/{urllib.parse.quote(task_id)}?wait=30",
        )
        status = poll.get("task_status")
        if status == "success":
            result = _request(
                base_url,
                api_key,
                "GET",
                f"/v1/result/{urllib.parse.quote(task_id)}",
            )
            if result.get("status") != "success":
                raise EmbeddedImageConversionError(
                    "Docling returned an unsuccessful result."
                )
            return result
        if status not in {"pending", "started"}:
            raise EmbeddedImageConversionError(
                "Docling embedded-image task failed."
            )
        time.sleep(0.25)
    raise EmbeddedImageConversionError(
        "Docling embedded-image task exceeded its timeout."
    )


def _request(
    base_url: str,
    api_key: str,
    method: str,
    path: str,
    body: bytes | None = None,
    content_type: str | None = None,
) -> dict[str, Any]:
    headers = {"X-Api-Key": api_key}
    if content_type:
        headers["Content-Type"] = content_type
    request = urllib.request.Request(
        base_url + path,
        data=body,
        headers=headers,
        method=method,
    )
    try:
        with urllib.request.urlopen(
            request,
            timeout=min(90, _timeout_seconds()),
        ) as response:
            value = json.loads(response.read())
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as error:
        raise EmbeddedImageConversionError(
            "Docling embedded-image request failed."
        ) from error
    if not isinstance(value, dict):
        raise EmbeddedImageConversionError(
            "Docling embedded-image response was not an object."
        )
    return value


def _multipart(
    filename: str,
    media_type: str,
    content: bytes,
) -> tuple[bytes, str]:
    boundary = "----advertified-" + uuid.uuid4().hex
    parts: list[bytes] = []

    def field(name: str, value: str) -> None:
        parts.extend([
            f"--{boundary}\r\n".encode(),
            f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode(),
            value.encode(),
            b"\r\n",
        ])

    parts.extend([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="files"; filename="{filename}"\r\n'.encode(),
        f"Content-Type: {media_type}\r\n\r\n".encode(),
        content,
        b"\r\n",
    ])
    for output_format in ("json", "text"):
        field("to_formats", output_format)
    field("image_export_mode", "placeholder")
    field("include_images", "false")
    field("do_ocr", "true")
    field("do_table_structure", "true")
    field("abort_on_error", "true")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def _decode_picture(picture: Any) -> tuple[str, bytes] | None:
    if not isinstance(picture, dict):
        return None
    image = picture.get("image")
    if not isinstance(image, dict):
        return None
    media_type = image.get("mimetype")
    uri = image.get("uri")
    if not isinstance(media_type, str) or not isinstance(uri, str):
        return None
    prefix = f"data:{media_type};base64,"
    if not uri.startswith(prefix):
        return None
    try:
        return media_type, base64.b64decode(uri[len(prefix):], validate=True)
    except (ValueError, binascii.Error):
        return None


def _with_lineage(
    items: Any,
    prefix: str,
    picture_number: int,
    structure_kind: str | None = None,
) -> list[dict[str, Any]]:
    result = []
    values = items if isinstance(items, list) else []
    for item_number, item in enumerate(values, start=1):
        if not isinstance(item, dict):
            continue
        copied = copy.deepcopy(item)
        child_page = page(copied)
        locator = f"{prefix};child-page={child_page}"
        if structure_kind:
            locator += f";{structure_kind}={item_number}"
        copied["_advertified_locator_prefix"] = locator
        copied["_advertified_virtual_page"] = picture_number * 10_000 + child_page
        result.append(copied)
    return result


def _picture_locator(
    document: dict[str, Any],
    picture: Any,
    picture_number: int,
) -> str:
    group_name = _parent_group_name(document, picture)
    if group_name:
        encoded = urllib.parse.quote(group_name, safe="")
        return f"docling:group={encoded};picture={picture_number}"
    return f"docling:picture={picture_number}"


def _parent_group_name(
    document: dict[str, Any],
    picture: Any,
) -> str | None:
    if not isinstance(picture, dict):
        return None
    parent = picture.get("parent")
    reference = parent.get("$ref") if isinstance(parent, dict) else None
    if not isinstance(reference, str) or not reference.startswith("#/groups/"):
        return None
    try:
        index = int(reference.rsplit("/", 1)[1])
        group = document.get("groups", [])[index]
    except (ValueError, IndexError, TypeError):
        return None
    name = group.get("name") if isinstance(group, dict) else None
    return name.strip() if isinstance(name, str) and name.strip() else None


def _extension(media_type: str) -> str:
    return {
        "image/jpeg": ".jpg",
        "image/png": ".png",
        "image/tiff": ".tiff",
        "image/webp": ".webp",
    }.get(media_type.casefold(), ".img")


def _timeout_seconds() -> int:
    try:
        value = int(os.environ.get("DOCLING_EMBEDDED_IMAGE_TIMEOUT_SECONDS", "120"))
    except ValueError:
        value = 120
    return min(600, max(10, value))


def _maximum_pictures() -> int:
    try:
        value = int(os.environ.get("DOCLING_EMBEDDED_IMAGE_MAX", "100"))
    except ValueError:
        value = 100
    return min(100, max(1, value))
