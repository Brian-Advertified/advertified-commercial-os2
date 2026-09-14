"""Local SSO refresh uses a private cache without modifying the host or copying other profiles."""
import hashlib
import importlib
import json
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.parametrize("modern", [True, False])
def test_selected_profile_is_scoped_and_refresh_cache_is_independent(monkeypatch, tmp_path, modern):
    monkeypatch.syspath_prepend(str(ROOT / "tools"))
    bootstrap = importlib.import_module("preview_sso_bootstrap")
    source = tmp_path / "host"
    cache = source / "sso" / "cache"
    cache.mkdir(parents=True)
    profile = "advertified-codex-audit"
    url = "https://example.invalid/start"
    key = "preview-session" if modern else url
    selected = "sso_session = preview-session\n" if modern else f"sso_start_url = {url}\nsso_region = us-east-1\n"
    text = (f"[profile {profile}]\n{selected}sso_account_id = 111111111111\n"
            "sso_role_name = Audit\nregion = us-east-1\n"
            "[profile unrelated]\ncredential_process = never-copy-this\n")
    if modern:
        text += f"[sso-session preview-session]\nsso_start_url = {url}\nsso_region = us-east-1\n"
    (source / "config").write_text(text, encoding="utf-8")
    name = hashlib.sha1(key.encode(), usedforsecurity=False).hexdigest() + ".json"
    original = json.dumps({"accessToken": "synthetic-test-only", "expiresAt": "2027-01-01T00:00:00Z"})
    (cache / name).write_text(original, encoding="utf-8")
    (cache / "unrelated.json").write_text('{}', encoding="utf-8")
    destination = tmp_path / "ephemeral"
    bootstrap.stage_profile(source, destination, profile)
    assert "unrelated" not in (destination / "config").read_text(encoding="utf-8")
    assert list((destination / "sso" / "cache").iterdir()) == [destination / "sso" / "cache" / name]
    (destination / "sso" / "cache" / name).write_text('{}', encoding="utf-8")
    assert (cache / name).read_text(encoding="utf-8") == original
    assert (source / "config").read_text(encoding="utf-8") == text
    with pytest.raises(RuntimeError, match="profile is missing"):
        bootstrap.stage_profile(source, tmp_path / "absent", "missing")


def test_static_default_profile_is_scoped_to_ephemeral_storage(monkeypatch, tmp_path):
    monkeypatch.syspath_prepend(str(ROOT / "tools"))
    bootstrap = importlib.import_module("preview_sso_bootstrap")
    source = tmp_path / "host-static"
    source.mkdir()
    (source / "config").write_text("[default]\nregion = us-east-1\noutput = json\n", encoding="utf-8")
    original_credentials = (
        "[default]\naws_access_key_id = TESTACCESS\naws_secret_access_key = TESTSECRET\n"
        "aws_session_token = TESTTOKEN\n[unrelated]\naws_access_key_id = OTHER\n"
        "aws_secret_access_key = OTHERSECRET\n"
    )
    (source / "credentials").write_text(original_credentials, encoding="utf-8")
    destination = tmp_path / "ephemeral-static"

    bootstrap.stage_profile(source, destination, "default")

    assert "unrelated" not in (destination / "credentials").read_text(encoding="utf-8")
    assert "TESTACCESS" in (destination / "credentials").read_text(encoding="utf-8")
    assert "us-east-1" in (destination / "config").read_text(encoding="utf-8")
    assert (source / "credentials").read_text(encoding="utf-8") == original_credentials
