"""Local-only API client for durable confidential-corpus extraction."""

from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any, Callable

import requests

RATE_LIMIT_RETRY_ATTEMPTS = 3
TRANSPORT_RETRY_ATTEMPTS = 12
TRANSPORT_RETRY_SECONDS = 5
ACTIVE_ATTEMPT_STATES = {"PENDING", "SUBMITTING", "RUNNING", "FAILED_RETRYABLE"}
REVIEW_REQUIRED = "REVIEW_REQUIRED"


class InventoryApi:
    def __init__(self, base_url: str, origin: str, tenant_id: str) -> None:
        self.base_url = base_url.rstrip("/")
        if not self.base_url.startswith(("http://127.0.0.1", "http://localhost")):
            raise ValueError("Corpus processing is restricted to the local API.")
        self.origin = origin
        self.tenant_id = tenant_id
        self.session = requests.Session()
        self.csrf_token = ""

    def start_session(self) -> None:
        status = self.request("GET", "/api/v1/session")
        if not status["authenticated"]:
            self.request("POST", "/api/v1/session", csrf=status["antiforgeryToken"])
            status = self.request("GET", "/api/v1/session")
        if not status["authenticated"]:
            raise RuntimeError("The local inventory session did not authenticate.")
        self.csrf_token = status["antiforgeryToken"]

    def create_import(
        self, source_path: Path, document: dict[str, Any], supplier_name: str,
    ) -> dict[str, Any]:
        with source_path.open("rb") as source:
            files = {"source": (source_path.name, source, document["mediaType"])}
            return self.request(
                "POST", self.tenant_path("/inventory-imports"),
                files=files, data={"supplierName": supplier_name},
                idempotency=f"corpus-{document['id']}-create",
                timeout=900,
            )

    def execute_import(self, import_id: str, version: int) -> dict[str, Any]:
        return self.request(
            "POST", self.tenant_path(f"/inventory-imports/{import_id}:execute"),
            idempotency=f"corpus-{import_id}-execute", version=version,
        )

    def retry_import(
        self, import_id: str, version: int, attempt_number: int, source_hash: str,
    ) -> dict[str, Any]:
        return self.request(
            "POST", self.tenant_path(f"/inventory-imports/{import_id}:retry-extraction"),
            json={"reason": "Explicit corpus recovery creates a new durable attempt."},
            idempotency=f"corpus-{source_hash}-retry-{attempt_number}", version=version,
        )

    def reproject_import(
        self, import_id: str, version: int, source_hash: str,
        attempt_number: int,
    ) -> dict[str, Any]:
        return self.request(
            "POST",
            self.tenant_path(
                f"/inventory-imports/{import_id}:reproject-extraction"
            ),
            json={
                "reason": (
                    "Authorised semantic recovery reprojects the retained "
                    "Docling artifact without resubmitting the source."
                )
            },
            idempotency=(
                f"corpus-{source_hash}-advertified-projection-v3-"
                f"attempt-{attempt_number}"
            ),
            version=version,
        )

    def wait_for_extraction(
        self, import_id: str, poll_seconds: int, max_wait_seconds: int,
        observe: Callable[[dict[str, Any]], None], checkpoint: Callable[[], None],
    ) -> dict[str, Any]:
        deadline = time.monotonic() + max_wait_seconds
        previous: tuple[str, str | None] | None = None
        while time.monotonic() < deadline:
            current = self.read_import(import_id)
            attempts = current.get("extractionAttempts") or []
            latest = max(
                attempts,
                key=lambda attempt: attempt["attemptNumber"],
                default=None,
            )
            marker = (current["status"], latest["status"] if latest else None)
            if marker != previous:
                observe({"importStatus": marker[0], "attempt": latest})
                checkpoint()
                print(json.dumps({"importId": import_id, "state": marker}), flush=True)
                previous = marker
            if current["status"] == REVIEW_REQUIRED:
                return current
            if (
                latest
                and latest["status"] not in ACTIVE_ATTEMPT_STATES
                and latest["status"] != "COMPLETED"
            ):
                raise RuntimeError(f"Extraction stopped in {latest['status']}.")
            time.sleep(poll_seconds)
        raise RuntimeError("Extraction did not finish within the bounded observation window.")

    def read_complete_import(self, import_id: str) -> dict[str, Any]:
        pages: list[dict[str, Any]] = []
        cursor: str | None = None
        summary: dict[str, Any] | None = None
        while True:
            suffix = "?pageSize=100" + (
                f"&cursor={requests.utils.quote(cursor)}" if cursor else "")
            page = self.request("GET", self.tenant_path(f"/inventory-imports/{import_id}{suffix}"))
            summary = summary or {key: value for key, value in page.items()
                                  if key not in {"candidates", "nextCandidateCursor"}}
            pages.extend(page["candidates"])
            cursor = page.get("nextCandidateCursor")
            if not cursor:
                return {**summary, "candidates": pages}

    def read_import(self, import_id: str) -> dict[str, Any]:
        return self.request(
            "GET", self.tenant_path(f"/inventory-imports/{import_id}?pageSize=1"))

    def tenant_path(self, suffix: str) -> str:
        return f"/api/v1/tenants/{self.tenant_id}{suffix}"

    def request(
        self, method: str, path: str, *, csrf: str | None = None,
        idempotency: str | None = None, version: int | None = None,
        timeout: int = 60, **kwargs: Any,
    ) -> dict[str, Any]:
        headers = {"Origin": self.origin}
        token = csrf or self.csrf_token
        if token:
            headers["X-CSRF-TOKEN"] = token
        if idempotency:
            headers["Idempotency-Key"] = idempotency
        if version is not None:
            headers["If-Match"] = f'"{version}"'
        response = self.send_with_rate_limit_retry(
            method, path, headers, timeout, **kwargs)
        if response.status_code == 403 and path != "/api/v1/session":
            response.close()
            self.start_session()
            headers["X-CSRF-TOKEN"] = self.csrf_token
            response = self.send_with_rate_limit_retry(
                method, path, headers, timeout, **kwargs)
        if not response.ok:
            raise RuntimeError(f"Local API returned HTTP {response.status_code}.")
        return response.json()

    def send_with_rate_limit_retry(
        self, method: str, path: str, headers: dict[str, str], timeout: int, **kwargs: Any,
    ) -> requests.Response:
        rate_limit_attempts = 0
        for transport_attempt in range(TRANSPORT_RETRY_ATTEMPTS):
            try:
                rewind_files(kwargs)
                response = self.session.request(
                    method, self.base_url + path, headers=headers,
                    timeout=timeout, **kwargs)
            except (requests.ConnectionError, requests.Timeout):
                if transport_attempt == TRANSPORT_RETRY_ATTEMPTS - 1:
                    raise
                time.sleep(TRANSPORT_RETRY_SECONDS)
                continue
            if response.status_code != 429:
                return response
            rate_limit_attempts += 1
            if rate_limit_attempts == RATE_LIMIT_RETRY_ATTEMPTS:
                return response
            retry_after = int(response.headers.get("Retry-After", "0"))
            response.close()
            if retry_after <= 0:
                raise RuntimeError("The local API rate limit omitted a retry interval.")
            wait_for_rate_limit(retry_after)
        raise RuntimeError("The local API transport retry was exhausted.")


def rewind_files(kwargs: dict[str, Any]) -> None:
    for value in (kwargs.get("files") or {}).values():
        if isinstance(value, tuple) and len(value) > 1 and hasattr(value[1], "seek"):
            value[1].seek(0)


def wait_for_rate_limit(seconds: int) -> None:
    remaining = seconds
    while remaining > 0:
        interval = min(remaining, 60)
        print(json.dumps({"rateLimited": True, "retryingInSeconds": remaining}), flush=True)
        time.sleep(interval)
        remaining -= interval
