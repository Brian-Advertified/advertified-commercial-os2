from pathlib import Path
import json


ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "api/src/Advertified.Commercial.Infrastructure/Inventory"


def test_inventory_acceptance_does_not_restore_corpus_or_filename_authority():
    policy_files = list(INVENTORY.glob("InventoryAcceptance*.cs"))
    policy_files += list(INVENTORY.glob("InventorySchema*.cs"))
    assert policy_files
    for path in policy_files:
        source = path.read_text(encoding="utf-8")
        assert "request.FileName" not in source, path
        assert "InventoryKnownSourcePolicy" not in source, path
        assert "HistoricalInventoryKnownSourceProjection" not in source, path
    for path in INVENTORY.glob("*.cs"):
        source = path.read_text(encoding="utf-8")
        for obsolete in ("KENA OUTDOOR", "DStvPositioningPhrases", "Welcome to DStv on Digital"):
            assert obsolete not in source, path
        assert "InferChannel(request.FileName)" not in source, path


def test_historical_supplier_projection_cannot_be_a_production_dependency():
    assert not (INVENTORY / "InventoryKnownSourcePolicy.cs").exists()
    assert not (INVENTORY / "InventorySourceContextProjection.cs").exists()
    assert not (ROOT / "api/tests/Advertified.Commercial.Api.Tests/HistoricalInventoryKnownSourceProjection.cs").exists()
    assert not (ROOT / "tools/build_inventory_physical_projection_bundle.py").exists()
    for path in (ROOT / "api/src").rglob("*.cs"):
        assert "HistoricalInventoryKnownSourceProjection" not in path.read_text(encoding="utf-8"), path


def test_application_and_regressions_do_not_depend_on_private_inventory():
    paths = list((ROOT / "api").rglob("*.cs"))
    paths += list((ROOT / "agent-runtime").glob("*.py"))
    for path in paths:
        if {"obj", "bin"}.intersection(path.parts):
            continue
        source = path.read_text(encoding="utf-8")
        assert '"inventory-corpus"' not in source, path
        assert '"source-manifest.json"' not in source, path
        assert "ALL_43_PHYSICALLY_CERTIFIED" not in source, path
    scripts = json.loads((ROOT / "web/package.json").read_text(encoding="utf-8"))["scripts"]
    assert not any("--document " in command or "--maximum 43" in command
                   for command in scripts.values())
    for name, command in scripts.items():
        if name in {"build", "dev", "start", "test", "bootstrap"}:
            assert "inventory-corpus" not in command
            assert "certify_inventory" not in command


def test_parallel_inventory_transcription_and_reimport_are_removed():
    for name in (
        "inventory_physical_transcriber", "inventory_physical_transcription_rows",
        "prepare_inventory_bedrock_certification", "run_inventory_bedrock_certification",
        "assemble_certified_inventory_products", "prepare_certified_inventory_upload",
        "upload_certified_inventory", "report_inventory_production_status",
        "inventory_finalization_evidence", "certify_dms_two_stage", "repair_dms_local",
        "publish_certified_inventory", "inventory_production_release_policy",
    ):
        assert not (ROOT / "tools" / f"{name}.py").exists()
