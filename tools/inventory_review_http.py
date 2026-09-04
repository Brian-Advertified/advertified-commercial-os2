"""OpenAPI-driven inventory review and publication client.

The adapter discovers the current API contract instead of duplicating route and
command shapes in corpus scripts.
"""

from __future__ import annotations

import inspect
import json
import uuid
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
OPENAPI = ROOT / "shared" / "contracts" / "openapi" / "advertified-commercial-api.v1.json"


class InventoryReviewHttp:
    def __init__(self, api: InventoryApi, tenant_id: str) -> None:
        self.api = api
        self.tenant_id = tenant_id
        self.contract = json.loads(OPENAPI.resolve(strict=True).read_text(encoding="utf-8"))
        self.review_path, self.review_operation = self._find_operation(
            candidate=True,
            words=("review", "approve", "decision"),
        )
        self.publish_path, self.publish_operation = self._find_operation(
            candidate=False,
            words=("publish",),
        )

    def review(
        self,
        candidate_id: str,
        candidate_version: int,
        decision: str,
        values: dict[str, Any],
        reason: str,
    ) -> dict[str, Any]:
        path = self._path(
            self.review_path,
            candidate_id=candidate_id,
        )
        context = {
            "candidateId": candidate_id,
            "expectedVersion": candidate_version,
            "decision": decision,
            "values": values,
            "reason": reason,
            "idempotencyKey": str(uuid.uuid4()),
        }
        body = self._request_body(self.review_operation, context)
        return self._request("POST", path, body)

    def publish(
        self,
        import_id: str,
        expected_version: int,
        reason: str,
    ) -> dict[str, Any]:
        path = self._path(self.publish_path, import_id=import_id)
        context = {
            "importId": import_id,
            "expectedVersion": expected_version,
            "reason": reason,
            "idempotencyKey": str(uuid.uuid4()),
        }
        body = self._request_body(self.publish_operation, context)
        return self._request("POST", path, body)

    def contract_summary(self) -> dict[str, Any]:
        return {
            "reviewPath": self.review_path,
            "reviewOperationId": self.review_operation.get("operationId"),
            "reviewBodySchema": self._body_schema(self.review_operation),
            "publishPath": self.publish_path,
            "publishOperationId": self.publish_operation.get("operationId"),
            "publishBodySchema": self._body_schema(self.publish_operation),
        }

    def _find_operation(
        self,
        *,
        candidate: bool,
        words: tuple[str, ...],
    ) -> tuple[str, dict[str, Any]]:
        matches: list[tuple[int, str, dict[str, Any]]] = []
        for path, path_item in (self.contract.get("paths") or {}).items():
            operation = path_item.get("post")
            if not isinstance(operation, dict):
                continue
            lower = " ".join([
                path.lower(),
                str(operation.get("operationId") or "").lower(),
                str(operation.get("summary") or "").lower(),
            ])
            has_candidate = "candidate" in lower and "{candidateid}" in path.lower()
            if candidate != has_candidate:
                continue
            score = sum(word in lower for word in words)
            if not candidate and "inventory-imports" not in lower:
                continue
            if score:
                matches.append((score, path, operation))
        if not matches:
            kind = "candidate review" if candidate else "import publication"
            raise RuntimeError(f"The OpenAPI contract has no {kind} operation.")
        _, path, operation = max(matches, key=lambda item: item[0])
        return path, operation

    def _path(
        self,
        template: str,
        *,
        candidate_id: str | None = None,
        import_id: str | None = None,
    ) -> str:
        result = template
        substitutions = {
            "tenantId": self.tenant_id,
            "candidateId": candidate_id,
            "importId": import_id,
        }
        for name, value in substitutions.items():
            if value is not None:
                result = result.replace("{" + name + "}", value)
                result = result.replace("{" + name.lower() + "}", value)
        if "{" in result:
            raise RuntimeError(f"Unresolved OpenAPI route parameters: {result}")
        return result

    def _request_body(
        self,
        operation: dict[str, Any],
        context: dict[str, Any],
    ) -> dict[str, Any]:
        schema = self._body_schema(operation)
        body = self._value_for_schema(schema, context, property_name="body")
        if not isinstance(body, dict):
            raise RuntimeError("The OpenAPI request body is not an object.")
        return body

    def _body_schema(self, operation: dict[str, Any]) -> dict[str, Any]:
        content = (
            operation.get("requestBody", {})
            .get("content", {})
        )
        media = content.get("application/json") or next(
            iter(content.values()), {}
        )
        schema = media.get("schema") or {}
        return self._resolve(schema)

    def _resolve(self, schema: dict[str, Any]) -> dict[str, Any]:
        if "$ref" not in schema:
            return schema
        node: Any = self.contract
        for part in schema["$ref"].removeprefix("#/").split("/"):
            node = node[part.replace("~1", "/").replace("~0", "~")]
        if not isinstance(node, dict):
            raise RuntimeError("OpenAPI schema reference did not resolve to an object.")
        return node

    def _value_for_schema(
        self,
        schema: dict[str, Any],
        context: dict[str, Any],
        *,
        property_name: str,
    ) -> Any:
        schema = self._resolve(schema)
        if "allOf" in schema:
            merged: dict[str, Any] = {}
            for item in schema["allOf"]:
                value = self._value_for_schema(
                    item, context, property_name=property_name
                )
                if isinstance(value, dict):
                    merged.update(value)
            return merged
        for union in ("oneOf", "anyOf"):
            if union in schema:
                errors: list[str] = []
                for option in schema[union]:
                    try:
                        return self._value_for_schema(
                            option, context, property_name=property_name
                        )
                    except RuntimeError as error:
                        errors.append(str(error))
                raise RuntimeError("; ".join(errors))
        schema_type = schema.get("type")
        if schema_type == "object" or "properties" in schema:
            result: dict[str, Any] = {}
            properties = schema.get("properties") or {}
            required = set(schema.get("required") or [])
            for name, child in properties.items():
                try:
                    value = self._property_value(name, child, context)
                except RuntimeError:
                    if name in required:
                        raise
                    continue
                if value is not _MISSING:
                    result[name] = value
            missing = required.difference(result)
            if missing:
                raise RuntimeError(
                    f"Could not construct required OpenAPI properties: {sorted(missing)}"
                )
            return result
        return self._scalar(property_name, schema, context)

    def _property_value(
        self,
        name: str,
        schema: dict[str, Any],
        context: dict[str, Any],
    ) -> Any:
        normalized = _normalize(name)
        direct = {
            "candidateid": "candidateId",
            "importid": "importId",
            "expectedversion": "expectedVersion",
            "version": "expectedVersion",
            "reason": "reason",
            "comment": "reason",
            "note": "reason",
            "rationale": "reason",
            "idempotencykey": "idempotencyKey",
            "commandid": "idempotencyKey",
            "values": "values",
            "candidatevalues": "values",
            "canonicalvalues": "values",
            "proposedvalues": "values",
            "updatedvalues": "values",
        }
        if normalized in direct and direct[normalized] in context:
            return context[direct[normalized]]
        if any(word in normalized for word in ("decision", "action", "disposition")):
            return self._decision(schema, str(context["decision"]))
        resolved = self._resolve(schema)
        if resolved.get("type") == "object" or "properties" in resolved:
            return self._value_for_schema(
                resolved, context, property_name=name
            )
        if "default" in resolved:
            return resolved["default"]
        if resolved.get("nullable"):
            return None
        return _MISSING

    def _decision(self, schema: dict[str, Any], requested: str) -> str:
        resolved = self._resolve(schema)
        allowed = [str(value) for value in resolved.get("enum") or []]
        requested_normalized = _normalize(requested)
        for value in allowed:
            if _normalize(value) == requested_normalized:
                return value
        aliases = {
            "APPROVE": ("approve", "approved", "accept", "accepted"),
            "EDIT": ("edit", "edited", "correct", "corrected", "update"),
            "REJECT": ("reject", "rejected", "decline", "declined"),
        }
        for alias in aliases.get(requested.upper(), (requested,)):
            for value in allowed:
                if _normalize(alias) in _normalize(value):
                    return value
        return requested

    def _scalar(
        self,
        name: str,
        schema: dict[str, Any],
        context: dict[str, Any],
    ) -> Any:
        value = self._property_value(name, schema, context)
        if value is _MISSING:
            raise RuntimeError(f"No value for scalar OpenAPI property {name}.")
        return value

    def _request(
        self,
        method: str,
        path: str,
        body: dict[str, Any],
    ) -> dict[str, Any]:
        signature = inspect.signature(self.api.request)
        names = list(signature.parameters)
        if len(names) >= 3:
            return self.api.request(method, path, body)
        raise RuntimeError("InventoryApi.request does not accept a request body.")


class _Missing:
    pass


_MISSING = _Missing()


def _normalize(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())
