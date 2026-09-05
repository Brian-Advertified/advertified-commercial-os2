"""Only synthetic bytes are hashed; no document extraction or provider is run."""

from pathlib import Path

import pytest

from tools.process_inventory_corpus import load_or_create_manifest
from tools.reproject_inventory_corpus import select_documents
from tools.inventory_physical_certification_report import build_register


@pytest.mark.parametrize("count", [1, 2, 7, 44])
def test_evaluation_manifest_accepts_arbitrary_collections(tmp_path: Path, count: int):
    source = tmp_path / "inputs"
    source.mkdir()
    for index in range(count):
        extension = ("pdf", "xlsx", "pptx")[index % 3]
        (source / f"unfamiliar-{index}.{extension}").write_bytes(f"fixture-{index}".encode())
    path = tmp_path / "evidence/source-manifest.json"
    manifest = load_or_create_manifest(source, path)
    assert manifest["documentCount"] == count
    assert load_or_create_manifest(source, path) == manifest
    assert len(select_documents(manifest, [], True, count)) == count
    first = manifest["documents"][0]["relativePath"]
    assert len(select_documents(manifest, [first], False, 1)) == 1
    with pytest.raises(ValueError, match="explicitly"):
        select_documents(manifest, [], False, count)


def test_replacing_collection_requires_new_evidence_version_not_new_code(tmp_path: Path):
    source = tmp_path / "inputs"
    source.mkdir()
    file = source / "arbitrary.pdf"
    file.write_bytes(b"first synthetic version")
    first_path = tmp_path / "first/source-manifest.json"
    first = load_or_create_manifest(source, first_path)
    file.write_bytes(b"replacement synthetic version with different content")
    with pytest.raises(ValueError, match="changed"):
        load_or_create_manifest(source, first_path)
    replacement = load_or_create_manifest(source, tmp_path / "replacement/source-manifest.json")
    assert first["datasetVersion"] != replacement["datasetVersion"]
    assert first["documents"][0]["sha256"] != replacement["documents"][0]["sha256"]


@pytest.mark.parametrize("count", [1, 3, 49])
def test_comparison_register_requires_exact_selected_sources_not_fixed_count(count):
    manifest = {"documents": [{"sha256": str(index)} for index in range(count)]}
    records = [{
        "source_hash": str(index), "passed": True, "candidate_count": 1,
        "expected_anchor_count": 1, "matched_anchor_count": 1,
        "unsupported_candidate_count": 0, "blocking_candidate_count": 0,
    } for index in range(count)]
    assert build_register(manifest, {}, records)["verdict"] == "PASS"
    assert build_register(manifest, {}, records[:-1])["verdict"] == "FAIL"
    assert build_register(manifest, {}, records + [records[0]])["verdict"] == "FAIL"
    records[0]["source_hash"] = "different-source"
    assert build_register(manifest, {}, records)["verdict"] == "FAIL"
