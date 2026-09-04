"""OpenAPI-driven client for certified supplier inventory uploads."""

from __future__ import annotations

import hashlib
import json
import mimetypes
import re
import time
import uuid
from pathlib import Path
from typing import Any

import requests

XLSX_MEDIA_TYPE = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
TERMINAL_IMPORT_STATES = {"REVIEW_REQUIRED", "COMPLETED", "FAILED", "FAILED_TERMINAL", "CANCELLED"}


class CertifiedInventoryApi:
    def __init__(
        self,
        base_url: str,
        origin: str,
        tenant_id: str,
        contract: dict[str, Any],
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.origin = origin
        self.tenant_id = tenant_id
        self.contract = contract
        self.session = requests.Session()
        self.csrf = ""

    def authenticate(self) -> None:
        response = self._raw("GET", "/api/v1/session")
        payload = response.json()
        if not payload.get("authenticated"):
            token = str(payload.get("antiforgeryToken") or "")
            if not token:
                raise RuntimeError("Session did not provide an antiforgery token.")
            response = self._raw("POST", "/api/v1/session", csrf=token)
            response.raise_for_status()
            payload = self._raw("GET", "/api/v1/session").json()
        self.csrf = str(payload.get("antiforgeryToken") or token_from_cookies(self.session))
        if not payload.get("authenticated"):
            raise RuntimeError("The local operator session could not authenticate.")

    def ensure_supplier(self, supplier_name: str) -> str | None:
        existing = self.find_supplier(supplier_name)
        if existing:
            return existing
        operation = self.find_operation(
            method="POST",
            path_predicate=lambda path: path.rstrip("/").endswith("/suppliers"),
            required=False,
        )
        if operation is None:
            return None
        context = {
            "supplierName": supplier_name,
            "name": supplier_name,
            "displayName": supplier_name,
            "idempotencyKey": deterministic_uuid("supplier:" + supplier_name),
        }
        body = build_json_body(operation, context)
        response = self.request_operation(operation, json_body=body)
        payload = unwrap(response.json())
        supplier_id = find_identifier(payload, ("supplierId", "id"))
        if not supplier_id:
            raise RuntimeError(f"Supplier creation did not return an ID for {supplier_name}.")
        return supplier_id

    def find_supplier(self, supplier_name: str) -> str | None:
        operations = [
            item for item in self.contract.get("operations") or []
            if item.get("method") == "GET" and "/suppliers" in str(item.get("path") or "")
        ]
        for operation in operations:
            if "{" in operation["path"]:
                continue
            params = {}
            for parameter in operation.get("parameters") or []:
                name = str(parameter.get("name") or "")
                if name.lower() in {"query", "search", "name", "suppliername"}:
                    params[name] = supplier_name
                elif parameter.get("required") and parameter.get("in") == "query":
                    params[name] = default_scalar(parameter.get("schema") or {}, {})
            response = self.request_operation(operation, params=params)
            if response.status_code >= 400:
                continue
            for item in flatten_objects(response.json()):
                name = str(item.get("name") or item.get("supplierName") or "")
                if normalize(name) == normalize(supplier_name):
                    return find_identifier(item, ("supplierId", "id"))
        return None

    def create_import(
        self,
        workbook: Path,
        supplier_name: str,
        supplier_id: str | None,
        idempotency_key: str,
    ) -> dict[str, Any]:
        operation = self.find_operation(
            method="POST",
            path_predicate=lambda path: path.rstrip("/").endswith("/inventory-imports"),
        )
        request_body = operation.get("requestBody") or {}
        content = request_body.get("content") or {}
        context = upload_context(
            workbook,
            supplier_name,
            supplier_id,
            idempotency_key,
        )
        if "multipart/form-data" in content:
            schema = (content["multipart/form-data"] or {}).get("schema") or {}
            files, data = build_multipart(schema, context, workbook)
            response = self.request_operation(
                operation,
                files=files,
                data=data,
            )
        elif "application/json" in content:
            body = build_from_schema(
                (content["application/json"] or {}).get("schema") or {},
                context,
            )
            response = self.request_operation(operation, json_body=body)
        else:
            raise RuntimeError("Inventory import upload media type is unsupported.")
        response.raise_for_status()
        payload = unwrap(response.json())
        import_id = find_identifier(payload, ("importId", "id"))
        if not import_id:
            raise RuntimeError("Inventory upload did not return an import ID.")
        return normalize_import(payload, import_id)

    def read_import(self, import_id: str, page_size: int = 100) -> dict[str, Any]:
        operation = self.find_operation(
            method="GET",
            path_predicate=lambda path: (
                "/inventory-imports/{" in path
                and not path.endswith(":publish")
                and ":" not in path.rsplit("/", 1)[-1]
            ),
        )
        response = self.request_operation(
            operation,
            path_values={"importId": import_id},
            params={"pageSize": page_size},
        )
        response.raise_for_status()
        return unwrap(response.json())

    def execute_import(
        self,
        import_id: str,
        version: int,
        idempotency_key: str,
    ) -> dict[str, Any]:
        operation = self.find_operation(
            method="POST",
            path_predicate=lambda path: path.endswith(":execute"),
        )
        context = {
            "expectedVersion": version,
            "idempotencyKey": idempotency_key,
            "importId": import_id,
        }
        body = build_json_body(operation, context)
        response = self.request_operation(
            operation,
            path_values={"importId": import_id},
            json_body=body,
        )
        response.raise_for_status()
        return unwrap(response.json())

    def review_candidate(
        self,
        import_id: str,
        candidate_id: str,
        expected_version: int,
        decision: str,
        reason: str,
        values: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        operation = self.find_operation(
            method="POST",
            path_predicate=lambda path: (
                "inventory" in path.lower()
                and "candidate" in path.lower()
                and "{candidateId" in path
                and (path.endswith(":review") or path.endswith(":approve") or path.endswith(":reject"))
            ),
        )
        context = {
            "importId": import_id,
            "candidateId": candidate_id,
            "expectedVersion": expected_version,
            "decision": decision,
            "decisionCode": decision,
            "reviewDecision": decision,
            "action": decision,
            "reason": reason,
            "notes": reason,
            "comment": reason,
            "values": values,
            "canonicalValues": values,
            "proposedValues": values,
            "idempotencyKey": deterministic_uuid(
                f"review:{import_id}:{candidate_id}:{decision}:{expected_version}"
            ),
        }
        body = build_json_body(operation, context)
        response = self.request_operation(
            operation,
            path_values={
                "importId": import_id,
                "candidateId": candidate_id,
            },
            json_body=body,
        )
        response.raise_for_status()
        return unwrap(response.json())

    def publish_import(
        self,
        import_id: str,
        expected_version: int,
    ) -> dict[str, Any]:
        operation = self.find_operation(
            method="POST",
            path_predicate=lambda path: path.endswith(":publish"),
        )
        context = {
            "importId": import_id,
            "expectedVersion": expected_version,
            "idempotencyKey": deterministic_uuid(
                f"publish:{import_id}:{expected_version}"
            ),
            "reason": "Publish only physically certified and Bedrock-validated inventory.",
        }
        body = build_json_body(operation, context)
        response = self.request_operation(
            operation,
            path_values={"importId": import_id},
            json_body=body,
        )
        response.raise_for_status()
        return unwrap(response.json())

    def wait_for_import(
        self,
        import_id: str,
        *,
        timeout_seconds: int = 1800,
        poll_seconds: int = 2,
    ) -> dict[str, Any]:
        deadline = time.monotonic() + timeout_seconds
        current: dict[str, Any] = {}
        while time.monotonic() < deadline:
            current = self.read_complete_import(import_id)
            status = str(current.get("status") or "")
            attempts = current.get("extractionAttempts") or []
            active = any(
                str(item.get("status") or "") in {"PENDING", "RUNNING", "CLAIMED"}
                for item in attempts
            )
            if status in TERMINAL_IMPORT_STATES and not active:
                return current
            time.sleep(poll_seconds)
        raise TimeoutError(f"Inventory import {import_id} did not reach a terminal state.")

    def read_complete_import(self, import_id: str) -> dict[str, Any]:
        first = self.read_import(import_id, 100)
        candidates = list(first.get("candidates") or [])
        cursor = first.get("nextCandidateCursor")
        while cursor:
            operation = self.find_operation(
                method="GET",
                path_predicate=lambda path: (
                    "/inventory-imports/{" in path
                    and ":" not in path.rsplit("/", 1)[-1]
                ),
            )
            response = self.request_operation(
                operation,
                path_values={"importId": import_id},
                params={"pageSize": 100, "cursor": cursor},
            )
            response.raise_for_status()
            page = unwrap(response.json())
            candidates.extend(page.get("candidates") or [])
            cursor = page.get("nextCandidateCursor")
        first["candidates"] = candidates
        return first

    def request_operation(
        self,
        operation: dict[str, Any],
        *,
        path_values: dict[str, Any] | None = None,
        params: dict[str, Any] | None = None,
        json_body: Any = None,
        files: dict[str, Any] | None = None,
        data: dict[str, Any] | None = None,
    ) -> requests.Response:
        path = operation["path"].replace("{tenantId}", self.tenant_id)
        for key, value in (path_values or {}).items():
            path = re.sub(r"\{" + re.escape(key) + r"(?::[^}]*)?\}", str(value), path)
        return self._raw(
            operation["method"],
            path,
            params=params,
            json_body=json_body,
            files=files,
            data=data,
            csrf=self.csrf,
        )

    def find_operation(
        self,
        *,
        method: str,
        path_predicate: Any,
        required: bool = True,
    ) -> dict[str, Any] | None:
        matches = [
            item for item in self.contract.get("operations") or []
            if item.get("method") == method and path_predicate(str(item.get("path") or ""))
        ]
        if len(matches) == 1:
            return matches[0]
        if not matches and not required:
            return None
        raise RuntimeError(
            f"Expected one {method} operation, found {len(matches)}."
        )

    def _raw(
        self,
        method: str,
        path: str,
        *,
        params: dict[str, Any] | None = None,
        json_body: Any = None,
        files: dict[str, Any] | None = None,
        data: dict[str, Any] | None = None,
        csrf: str | None = None,
    ) -> requests.Response:
        headers = {"Origin": self.origin}
        if csrf:
            headers["X-CSRF-TOKEN"] = csrf
        return self.session.request(
            method,
            self.base_url + path,
            params=params,
            json=json_body,
            files=files,
            data=data,
            headers=headers,
            timeout=120,
        )


def build_json_body(operation: dict[str, Any], context: dict[str, Any]) -> Any:
    content = (operation.get("requestBody") or {}).get("content") or {}
    schema = ((content.get("application/json") or {}).get("schema") or {})
    return build_from_schema(schema, context)


def build_from_schema(schema: dict[str, Any], context: dict[str, Any], name: str = "") -> Any:
    nullable = bool(schema.get("nullable")) or schema.get("type") == ["string", "null"]
    if name and lookup_context(name, context) is not None:
        return lookup_context(name, context)
    enum = schema.get("enum")
    if enum:
        preferred = preferred_enum(name, enum)
        return preferred
    schema_type = schema.get("type")
    if schema_type == "object" or "properties" in schema:
        result = {}
        required = set(schema.get("required") or [])
        for key, child in (schema.get("properties") or {}).items():
            known = lookup_context(key, context)
            if known is not None:
                result[key] = known
            elif key in required:
                result[key] = build_from_schema(child, context, key)
        return result
    if schema_type == "array":
        return []
    if schema_type in {"integer", "number"}:
        return 0
    if schema_type == "boolean":
        return True
    if nullable:
        return None
    if str(schema.get("format") or "") == "uuid":
        return str(uuid.uuid4())
    if name.lower().endswith("idempotencykey"):
        return str(uuid.uuid4())
    return default_string(name)


def build_multipart(
    schema: dict[str, Any],
    context: dict[str, Any],
    workbook: Path,
) -> tuple[dict[str, Any], dict[str, Any]]:
    files: dict[str, Any] = {}
    data: dict[str, Any] = {}
    required = set(schema.get("required") or [])
    for key, child in (schema.get("properties") or {}).items():
        if child.get("format") == "binary":
            files[key] = (
                workbook.name,
                workbook.read_bytes(),
                XLSX_MEDIA_TYPE,
            )
            continue
        value = lookup_context(key, context)
        if value is None and key in required:
            value = build_from_schema(child, context, key)
        if value is not None:
            data[key] = str(value).lower() if isinstance(value, bool) else str(value)
    if not files:
        raise RuntimeError("Inventory import contract contains no binary file field.")
    return files, data


def upload_context(
    workbook: Path,
    supplier_name: str,
    supplier_id: str | None,
    idempotency_key: str,
) -> dict[str, Any]:
    return {
        "file": workbook,
        "fileName": workbook.name,
        "supplierName": supplier_name,
        "name": supplier_name,
        "supplierId": supplier_id,
        "declaredMediaType": XLSX_MEDIA_TYPE,
        "mediaType": XLSX_MEDIA_TYPE,
        "contentType": XLSX_MEDIA_TYPE,
        "documentClass": "XLSX",
        "idempotencyKey": idempotency_key,
    }


def lookup_context(name: str, context: dict[str, Any]) -> Any:
    normalized = normalize(name)
    for key, value in context.items():
        if normalize(key) == normalized and value is not None:
            return value
    return None


def preferred_enum(name: str, values: list[Any]) -> Any:
    strings = [str(value) for value in values]
    normalized = normalize(name)
    preferences = {
        "decision": ("APPROVE", "APPROVED"),
        "reviewdecision": ("APPROVE", "APPROVED"),
        "action": ("APPROVE",),
        "documentclass": ("XLSX",),
    }
    for preferred in preferences.get(normalized, ()):
        if preferred in strings:
            return preferred
    return values[0]


def default_scalar(schema: dict[str, Any], context: dict[str, Any]) -> Any:
    return build_from_schema(schema, context)


def default_string(name: str) -> str:
    normalized = normalize(name)
    if "reason" in normalized or "note" in normalized or "comment" in normalized:
        return "Certified against immutable physical source evidence and validated Bedrock output."
    if "idempotency" in normalized:
        return str(uuid.uuid4())
    raise RuntimeError(f"Cannot safely infer required upload field {name!r}.")


def normalize_import(payload: dict[str, Any], import_id: str) -> dict[str, Any]:
    result = dict(payload)
    result.setdefault("id", import_id)
    result.setdefault("importId", import_id)
    return result


def unwrap(value: Any) -> Any:
    current = value
    for _ in range(4):
        if not isinstance(current, dict):
            return current
        for key in ("data", "result", "value"):
            if key in current and isinstance(current[key], (dict, list)):
                current = current[key]
                break
        else:
            return current
    return current


def flatten_objects(value: Any) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    if isinstance(value, dict):
        result.append(value)
        for child in value.values():
            result.extend(flatten_objects(child))
    elif isinstance(value, list):
        for child in value:
            result.extend(flatten_objects(child))
    return result


def find_identifier(value: Any, keys: tuple[str, ...]) -> str | None:
    for item in flatten_objects(value):
        for key in keys:
            candidate = item.get(key)
            if candidate and re.fullmatch(r"[0-9a-fA-F-]{32,36}", str(candidate)):
                return str(candidate)
    return None


def deterministic_uuid(value: str) -> str:
    return str(uuid.UUID(hashlib.md5(value.encode(), usedforsecurity=False).hexdigest()))


def token_from_cookies(session: requests.Session) -> str:
    for cookie in session.cookies:
        if "csrf" in cookie.name.lower() or "antiforgery" in cookie.name.lower():
            return cookie.value
    return ""


def normalize(value: str) -> str:
    return "".join(char.lower() for char in value if char.isalnum())
