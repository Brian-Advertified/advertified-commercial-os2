"""Export resolved OpenAPI operations used by certified inventory upload."""
from __future__ import annotations
import json
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
OPENAPI = ROOT / "shared" / "contracts" / "openapi" / "advertified-commercial-api.v1.json"
OUTPUT = ROOT / "artifacts" / "inventory-corpus" / "certified-upload" / "api-contract.json"

KEYWORDS = (
    "inventory-imports", "inventory-candidates", "inventory-products",
    "suppliers", "publish", "review", "approve",
)

def main() -> int:
    document = read_json(OPENAPI)
    selected = []
    for path, operations in (document.get("paths") or {}).items():
        if not any(keyword in path.lower() for keyword in KEYWORDS):
            continue
        for method, operation in operations.items():
            if method.lower() not in {"get", "post", "put", "patch", "delete"} or not isinstance(operation, dict):
                continue
            selected.append({
                "path": path,
                "method": method.upper(),
                "operationId": operation.get("operationId"),
                "summary": operation.get("summary"),
                "parameters": [resolve_parameter(item, document) for item in operation.get("parameters") or []],
                "requestBody": resolve_request_body(operation.get("requestBody"), document),
                "responses": {
                    code: resolve_response(value, document)
                    for code, value in (operation.get("responses") or {}).items()
                    if str(code).startswith("2")
                },
            })
    payload = {
        "schemaVersion": "advertified.inventory-upload-openapi-contract.v1",
        "operationCount": len(selected),
        "operations": selected,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({"operationCount": len(selected), "output": str(OUTPUT.relative_to(ROOT))}, indent=2))
    return 0


def resolve_parameter(value: Any, document: dict[str, Any]) -> Any:
    item = resolve_ref(value, document)
    if not isinstance(item, dict):
        return item
    return {
        "name": item.get("name"),
        "in": item.get("in"),
        "required": item.get("required"),
        "schema": resolve_schema(item.get("schema"), document),
    }


def resolve_request_body(value: Any, document: dict[str, Any]) -> Any:
    item = resolve_ref(value, document)
    if not isinstance(item, dict):
        return item
    return {
        "required": item.get("required"),
        "content": {
            media: {"schema": resolve_schema(content.get("schema"), document)}
            for media, content in (item.get("content") or {}).items()
        },
    }


def resolve_response(value: Any, document: dict[str, Any]) -> Any:
    item = resolve_ref(value, document)
    if not isinstance(item, dict):
        return item
    return {
        "description": item.get("description"),
        "content": {
            media: {"schema": resolve_schema(content.get("schema"), document)}
            for media, content in (item.get("content") or {}).items()
        },
    }


def resolve_schema(value: Any, document: dict[str, Any], depth: int = 0) -> Any:
    if depth > 8:
        return {"truncated": True}
    item = resolve_ref(value, document)
    if not isinstance(item, dict):
        return item
    result = {key: item.get(key) for key in ("type", "format", "description", "nullable", "enum", "default", "required") if key in item}
    if "properties" in item:
        result["properties"] = {
            key: resolve_schema(child, document, depth + 1)
            for key, child in item["properties"].items()
        }
    if "items" in item:
        result["items"] = resolve_schema(item["items"], document, depth + 1)
    for combinator in ("oneOf", "anyOf", "allOf"):
        if combinator in item:
            result[combinator] = [resolve_schema(child, document, depth + 1) for child in item[combinator]]
    return result


def resolve_ref(value: Any, document: dict[str, Any]) -> Any:
    if not isinstance(value, dict) or "$ref" not in value:
        return value
    result: Any = document
    for part in value["$ref"].removeprefix("#/").split("/"):
        result = result[part.replace("~1", "/").replace("~0", "~")]
    return result


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value

if __name__ == "__main__":
    raise SystemExit(main())
