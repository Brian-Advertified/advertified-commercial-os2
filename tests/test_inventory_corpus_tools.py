"""Acceptance tests for confidential corpus preparation and evaluation controls."""

from __future__ import annotations

import importlib.util
import io
import json
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]


def load_tool(name: str):
    path = REPO_ROOT / "tools" / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def create_corpus(root: Path) -> None:
    for extension, count in ((".pdf", 33), (".pptx", 8), (".xlsx", 2)):
        for index in range(count):
            (root / f"source-{extension[1:]}-{index:02d}{extension}").write_bytes(
                f"{extension}:{index}".encode()
            )


def test_manifest_records_exact_corpus_and_deterministic_holdout(tmp_path: Path) -> None:
    tool = load_tool("process_inventory_corpus")
    source = tmp_path / "source"
    evidence = tmp_path / "evidence"
    source.mkdir()
    create_corpus(source)

    manifest = tool.load_or_create_manifest(source, evidence / "source-manifest.json")

    assert manifest["documentCount"] == 43
    assert manifest["expectedPaidAiCostUsd"] == 0.0
    holdout = [item for item in manifest["documents"] if item["partition"] == "holdout"]
    assert len(holdout) == 9
    assert {item["extension"] for item in holdout} == {".pdf", ".pptx", ".xlsx"}


def test_cached_manifest_rejects_changed_source_without_rehashing_unchanged(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
) -> None:
    tool = load_tool("process_inventory_corpus")
    source = tmp_path / "source"
    evidence = tmp_path / "evidence"
    source.mkdir()
    create_corpus(source)
    path = evidence / "source-manifest.json"
    tool.load_or_create_manifest(source, path)
    monkeypatch.setattr(tool, "hash_file", lambda _: pytest.fail("unexpected rehash"))
    assert tool.load_or_create_manifest(source, path)["documentCount"] == 43

    changed = next(source.glob("*.pdf"))
    changed.write_bytes(b"changed")
    with pytest.raises(ValueError, match="immutable source corpus changed"):
        tool.load_or_create_manifest(source, path)


def test_local_session_refreshes_antiforgery_token_after_sign_in(monkeypatch) -> None:
    tool = load_tool("process_inventory_corpus")
    client = tool.InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", "tenant")
    responses = iter([
        {"authenticated": False, "antiforgeryToken": "anonymous"},
        {"authenticated": True, "antiforgeryToken": "still-anonymous"},
        {"authenticated": True, "antiforgeryToken": "authenticated"},
    ])
    calls = []

    def request(method, path, **kwargs):
        calls.append((method, path, kwargs))
        return next(responses)

    monkeypatch.setattr(client, "request", request)
    client.start_session()

    assert [call[:2] for call in calls] == [
        ("GET", "/api/v1/session"),
        ("POST", "/api/v1/session"),
        ("GET", "/api/v1/session"),
    ]
    assert calls[1][2]["csrf"] == "anonymous"
    assert client.csrf_token == "authenticated"


def test_local_api_honours_rate_limit_retry_interval(monkeypatch) -> None:
    tool = load_tool("process_inventory_corpus")
    client = tool.InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", "tenant")
    limited = tool.requests.Response()
    limited.status_code = 429
    limited.headers["Retry-After"] = "120"
    limited._content_consumed = True
    accepted = tool.requests.Response()
    accepted.status_code = 200
    accepted._content = b"{}"
    responses = iter([limited, accepted])
    waits = []
    monkeypatch.setattr(client.session, "request", lambda *args, **kwargs: next(responses))
    monkeypatch.setattr(tool.time, "sleep", waits.append)

    response = client.send_with_rate_limit_retry("POST", "/test", {}, 1)

    assert response.status_code == 200
    assert waits == [60, 60]


def test_local_api_recovers_from_a_bounded_restart_window(monkeypatch) -> None:
    tool = load_tool("process_inventory_corpus")
    client = tool.InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", "tenant")
    accepted = tool.requests.Response()
    accepted.status_code = 200
    responses = iter([tool.requests.ConnectionError("offline"), accepted])
    waits = []

    def request(*args, **kwargs):
        response = next(responses)
        if isinstance(response, Exception):
            raise response
        return response

    monkeypatch.setattr(client.session, "request", request)
    monkeypatch.setattr(tool.time, "sleep", waits.append)

    response = client.send_with_rate_limit_retry("GET", "/test", {}, 1)

    assert response.status_code == 200
    assert waits == [5]


def test_local_api_refreshes_csrf_after_container_restart(monkeypatch) -> None:
    tool = load_tool("process_inventory_corpus")
    client = tool.InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", "tenant")
    client.csrf_token = "stale"
    forbidden = tool.requests.Response()
    forbidden.status_code = 403
    forbidden._content_consumed = True
    accepted = tool.requests.Response()
    accepted.status_code = 200
    accepted._content = b"{}"
    responses = iter([forbidden, accepted])
    headers = []

    def send(method, path, supplied_headers, timeout, **kwargs):
        headers.append(dict(supplied_headers))
        return next(responses)

    monkeypatch.setattr(client, "send_with_rate_limit_retry", send)
    monkeypatch.setattr(client, "start_session", lambda: setattr(client, "csrf_token", "fresh"))

    assert client.request("POST", "/test") == {}
    assert [item["X-CSRF-TOKEN"] for item in headers] == ["stale", "fresh"]


def test_local_api_rewinds_upload_before_transport_retry(monkeypatch) -> None:
    tool = load_tool("process_inventory_corpus")
    client = tool.InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", "tenant")
    source = io.BytesIO(b"inventory")
    positions = []
    accepted = tool.requests.Response()
    accepted.status_code = 200

    def request(*args, **kwargs):
        stream = kwargs["files"]["source"][1]
        positions.append(stream.tell())
        stream.read()
        if len(positions) == 1:
            raise tool.requests.ConnectionError("restart")
        return accepted

    monkeypatch.setattr(client.session, "request", request)
    monkeypatch.setattr(tool.time, "sleep", lambda _: None)

    response = client.send_with_rate_limit_retry(
        "POST", "/test", {}, 1, files={"source": ("file.pdf", source)})

    assert response.status_code == 200
    assert positions == [0, 0]


def test_manifest_checkpoint_retries_transient_windows_sharing_violation(
    monkeypatch, tmp_path: Path,
) -> None:
    tool = load_tool("process_inventory_corpus")
    destination = tmp_path / "manifest.json"
    actual_replace = tool.os.replace
    calls = 0

    def replace(source, target) -> None:
        nonlocal calls
        calls += 1
        if calls < 3:
            raise PermissionError("transient sharing violation")
        actual_replace(source, target)

    monkeypatch.setattr(tool.os, "replace", replace)
    monkeypatch.setattr(tool.time, "sleep", lambda _: None)

    tool.write_json(destination, {"documentCount": 43})

    assert calls == 3
    assert json.loads(destination.read_text(encoding="utf-8")) == {"documentCount": 43}


def test_evaluator_requires_the_verified_document_count() -> None:
    evaluator = load_tool("evaluate_inventory_extraction")
    with pytest.raises(ValueError, match="exactly 43 documents"):
        evaluator.validate_manifest({"datasetVersion": "v1", "documents": [{}] * 42})


def test_collector_refuses_to_infer_an_unqueued_import(tmp_path: Path) -> None:
    collector = load_tool("collect_inventory_corpus")
    manifest = {
        "documents": [{
            "id": "a" * 64,
            "relativePath": "missing.pdf",
            "processing": {"state": "pending"},
        }],
    }

    with pytest.raises(RuntimeError, match="were not queued"):
        collector.collect(
            manifest, tmp_path / "manifest.json", tmp_path,
            client=None, poll_seconds=5, max_wait_seconds=10,
        )
