"""Run the canonical local projection through the paused verification route."""

from __future__ import annotations

import argparse
import hashlib
import http.cookiejar
import json
import mimetypes
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from inventory_evidence_binding import application_binding


EXPECTED_PROJECTION = "advertified-projection/4.0.0"
ORIGIN = "http://localhost:3017"


def read_json(response: Any) -> dict[str, Any]:
    return json.loads(response.read().decode("utf-8"))


def open_json(
    opener: urllib.request.OpenerDirector,
    request: urllib.request.Request,
    timeout: int = 30,
) -> dict[str, Any]:
    try:
        with opener.open(request, timeout=timeout) as response:
            return read_json(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(
            f"Verification HTTP {error.code}: {detail}"
        ) from error


def authenticated_opener(base_url: str) -> tuple[urllib.request.OpenerDirector, str]:
    cookies = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(cookies)
    )
    session = open_json(
        opener, urllib.request.Request(f"{base_url}/api/v1/session")
    )
    request = urllib.request.Request(
        f"{base_url}/api/v1/session",
        data=b"",
        method="POST",
        headers={
            "Origin": ORIGIN,
            "X-CSRF-TOKEN": session["antiforgeryToken"],
        },
    )
    open_json(opener, request)
    authenticated = open_json(
        opener, urllib.request.Request(f"{base_url}/api/v1/session")
    )
    if not authenticated.get("authenticated"):
        raise RuntimeError("Local verification session did not authenticate.")
    return opener, authenticated["antiforgeryToken"]


def multipart_source(path: Path) -> tuple[bytes, str]:
    boundary = "advertified-" + uuid.uuid4().hex
    media_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    prefix = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="source"; filename="{path.name}"\r\n'
        f"Content-Type: {media_type}\r\n\r\n"
    ).encode("utf-8")
    body = prefix + path.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
    return body, f"multipart/form-data; boundary={boundary}"


def verify_source(
    opener: urllib.request.OpenerDirector,
    token: str,
    base_url: str,
    tenant_id: str,
    source: Path,
) -> dict[str, Any]:
    body, content_type = multipart_source(source)
    request = urllib.request.Request(
        f"{base_url}/internal/tenants/{tenant_id}/inventory-projection-verification",
        data=body,
        method="POST",
        headers={
            "Accept": "application/json",
            "Content-Type": content_type,
            "Origin": ORIGIN,
            "X-CSRF-TOKEN": token,
        },
    )
    return open_json(opener, request, timeout=1800)


def source_records(manifest: dict[str, Any]) -> list[dict[str, str]]:
    return [
        {
            "relative_path": item["relative_path"],
            "source_hash": item["content_hash"],
        }
        for item in manifest["files"]
    ]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, payload: dict[str, Any]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def valid_cached(path: Path, source_hash: str, binding_hash: str) -> bool:
    if not path.exists():
        return False
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return False
    response = payload.get("response", {})
    return (
        payload.get("binding_sha256") == binding_hash
        and response.get("sourceHash") == source_hash
        and EXPECTED_PROJECTION in response.get("projectionVersion", "")
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:5197")
    parser.add_argument(
        "--tenant-id", default="10000000-0000-0000-0000-000000000020"
    )
    parser.add_argument("--api-image", required=True)
    parser.add_argument("--maximum-new", type=int)
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[1]
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    binding = application_binding(repository)
    binding["api_image"] = args.api_image
    binding_hash = hashlib.sha256(
        json.dumps(binding, sort_keys=True).encode("utf-8")
    ).hexdigest()
    args.output.mkdir(parents=True, exist_ok=True)
    opener, token = authenticated_opener(args.base_url.rstrip("/"))
    completed = 0
    records = source_records(manifest)
    for position, record in enumerate(records, start=1):
        destination = args.output / f"{record['source_hash']}.json"
        if valid_cached(destination, record["source_hash"], binding_hash):
            print(json.dumps({"position": position, "state": "cached"}), flush=True)
            continue
        if args.maximum_new is not None and completed >= args.maximum_new:
            break
        source = (args.source / record["relative_path"]).resolve(strict=True)
        if sha256(source) != record["source_hash"]:
            raise ValueError(f"Source hash mismatch: {record['relative_path']}")
        response = verify_source(
            opener, token, args.base_url.rstrip("/"), args.tenant_id, source
        )
        if response.get("sourceHash") != record["source_hash"]:
            raise ValueError(f"Response hash mismatch: {record['relative_path']}")
        if EXPECTED_PROJECTION not in response.get("projectionVersion", ""):
            raise ValueError(f"Projection version mismatch: {record['relative_path']}")
        write_json(destination, {
            "generated_at_utc": datetime.now(timezone.utc).isoformat(),
            "relative_path": record["relative_path"],
            "binding_sha256": binding_hash,
            "implementation_binding": binding,
            "response": response,
        })
        completed += 1
        print(json.dumps({
            "position": position,
            "source": record["relative_path"],
            "rows": response["extractedRowCount"],
            "candidates": len(response["projectedCandidates"]),
        }), flush=True)


if __name__ == "__main__":
    main()
