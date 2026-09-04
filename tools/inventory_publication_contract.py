"""OpenAPI discovery and safe request construction for inventory publication."""

from __future__ import annotations

import uuid
from typing import Any
from urllib.parse import quote


class ContractError(RuntimeError):
    pass


def discover_operation(
    contract: dict[str, Any],
    *,
    path_terms: tuple[str, ...],
    operation_terms: tuple[str, ...],
) -> dict[str, Any]:
    matches = []
    for path, path_item in (contract.get("paths") or {}).items():
        if not all(term.lower() in path.lower() for term in path_terms):
            continue
        for method, operation in path_item.items():
            if method.lower() not in {
                "get", "post", "put", "patch", "delete"
            }:
                continue
            operation_id = str(operation.get("operationId") or "")
            haystack = (path + " " + operation_id).lower()
            if all(term.lower() in haystack for term in operation_terms):
                matches.append({
                    "path": path,
                    "method": method.upper(),
                    "operation": operation,
                })
    if len(matches) != 1:
        raise ContractError(
            f"Expected exactly one operation for {path_terms}/{operation_terms}; "
            f"found {len(matches)}."
        )
    return matches[0]


def request_body(
    contract: dict[str, Any],
    operation: dict[str, Any],
    *,
    expected_version: Any,
    decision: str | None,
    values: dict[str, Any] | None,
    source_hash: str,
) -> dict[str, Any]:
    schema = operation_schema(contract, operation)
    required = schema.get("required") or []
    properties = schema.get("properties") or {}
    result: dict[str, Any] = {}
    for name in properties:
        normalized = normalize(name)
        if normalized in {
            "expectedversion",
            "expectedimportversion",
            "expectedcandidateversion",
            "version",
        }:
            result[name] = int(expected_version)
        elif normalized in {
            "decision",
            "decisioncode",
            "reviewdecision",
            "reviewdecisioncode",
            "action",
            "outcome",
        } and decision:
            result[name] = decision
        elif normalized in {
            "values", "candidatevalues", "canonicalvalues", "proposedvalues",
            "editedvalues", "approvedvalues", "patch",
        } and values is not None:
            result[name] = values
        elif normalized in {
            "reason", "comment", "notes", "note", "reviewnotes", "reviewnote"
        }:
            result[name] = (
                "Approved after 43-file physical and Bedrock certification."
            )
        elif normalized == "sourcehash":
            result[name] = source_hash
        elif normalized in {"idempotencykey", "requestid", "commandid"}:
            result[name] = str(uuid.uuid4())
    missing = [name for name in required if name not in result]
    if missing:
        raise ContractError(
            "Cannot safely construct request; unsupported required properties: "
            + ", ".join(missing)
        )
    return result


def operation_schema(
    contract: dict[str, Any],
    operation: dict[str, Any],
) -> dict[str, Any]:
    request = operation.get("requestBody") or {}
    content = request.get("content") or {}
    media = content.get("application/json") or next(
        iter(content.values()), {}
    )
    return resolve_schema(contract, media.get("schema") or {})


def resolve_schema(
    contract: dict[str, Any],
    schema: dict[str, Any],
) -> dict[str, Any]:
    reference = schema.get("$ref")
    if "allOf" in schema:
        merged: dict[str, Any] = {"properties": {}, "required": []}
        for item in schema.get("allOf") or []:
            resolved = resolve_schema(contract, item)
            merged["properties"].update(resolved.get("properties") or {})
            merged["required"].extend(resolved.get("required") or [])
        merged["required"] = list(dict.fromkeys(merged["required"]))
        return merged
    if not reference:
        return schema
    value: Any = contract
    for segment in reference.removeprefix("#/").split("/"):
        value = value[segment.replace("~1", "/").replace("~0", "~")]
    if not isinstance(value, dict):
        raise ContractError(f"Schema reference is not an object: {reference}")
    return value


def fill_path(path: str, **values: str) -> str:
    result = path
    for name, value in values.items():
        result = result.replace("{" + name + "}", quote(str(value), safe=""))
    if "{" in result or "}" in result:
        raise ContractError(f"Unresolved API path template: {result}")
    return result


def normalize(value: str) -> str:
    return "".join(
        character.lower() for character in value if character.isalnum()
    )
