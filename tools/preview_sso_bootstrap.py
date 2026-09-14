"""Stage one selected AWS profile in private ephemeral storage for local preview."""
from __future__ import annotations

import configparser
import hashlib
import json
import os
from pathlib import Path

PROFILE_KEYS = ("sso_session", "sso_account_id", "sso_role_name", "sso_start_url", "sso_region", "region", "output")
SESSION_KEYS = ("sso_start_url", "sso_region", "sso_registration_scopes")
CREDENTIAL_KEYS = ("aws_access_key_id", "aws_secret_access_key", "aws_session_token")
MAX_CACHE_BYTES = 1_048_576
MAX_CREDENTIAL_BYTES = 65_536


def _write_scoped_config(destination: Path, section: str, values: dict[str, str]) -> None:
    scoped = configparser.RawConfigParser()
    scoped[section] = values
    with (destination / "config").open("x", encoding="utf-8") as output:
        scoped.write(output)
    os.chmod(destination / "config", 0o600)


def _stage_static_credentials(source: Path, destination: Path, profile: str,
                              config: configparser.RawConfigParser, section: str) -> None:
    credentials_path = source / "credentials"
    if not credentials_path.is_file() or credentials_path.stat().st_size > MAX_CREDENTIAL_BYTES:
        raise RuntimeError("The selected local AWS credential profile is missing or invalid.")
    credentials = configparser.RawConfigParser()
    credentials.read(credentials_path, encoding="utf-8-sig")
    credential_section = "default" if profile == "default" else profile
    if credential_section not in credentials:
        raise RuntimeError("The selected local AWS credential profile is missing.")
    selected = credentials[credential_section]
    if not all(selected.get(key) for key in ("aws_access_key_id", "aws_secret_access_key")):
        raise RuntimeError("The selected local AWS credential profile is incomplete.")
    destination.mkdir(mode=0o700, parents=True, exist_ok=False)
    _write_scoped_config(destination, section, {
        key: config[section][key] for key in ("region", "output") if key in config[section]
    })
    scoped_credentials = configparser.RawConfigParser()
    scoped_credentials[credential_section] = {
        key: selected[key] for key in CREDENTIAL_KEYS if key in selected
    }
    with (destination / "credentials").open("x", encoding="utf-8") as output:
        scoped_credentials.write(output)
    os.chmod(destination / "credentials", 0o600)


def stage_profile(source: Path, destination: Path, profile: str) -> None:
    config = configparser.RawConfigParser()
    config.read(source / "config", encoding="utf-8-sig")
    section = "default" if profile == "default" else f"profile {profile}"
    if section not in config:
        raise RuntimeError("The selected local AWS profile is missing; refresh AWS sign-in or credentials.")
    selected = config[section]
    session_name = selected.get("sso_session")
    has_sso = bool(session_name or selected.get("sso_start_url") or selected.get("sso_account_id"))
    if not has_sso:
        _stage_static_credentials(source, destination, profile, config, section)
        return

    session_section = f"sso-session {session_name}" if session_name else None
    if session_section and session_section not in config:
        raise RuntimeError("The selected local SSO session is missing.")
    cache_key = session_name or selected.get("sso_start_url")
    if not cache_key or not all(selected.get(key) for key in ("sso_account_id", "sso_role_name")):
        raise RuntimeError("Local preview requires an explicitly selected SSO role.")
    cache_name = hashlib.sha1(cache_key.encode("utf-8"), usedforsecurity=False).hexdigest() + ".json"
    cache_source = source / "sso" / "cache" / cache_name
    if not cache_source.is_file() or cache_source.stat().st_size > MAX_CACHE_BYTES:
        raise RuntimeError("The selected SSO token cache is missing or invalid; refresh AWS sign-in.")
    token_bytes = cache_source.read_bytes()
    if not isinstance(json.loads(token_bytes).get("accessToken"), str):
        raise RuntimeError("The selected SSO token cache is invalid; refresh AWS sign-in.")
    destination.mkdir(mode=0o700, parents=True, exist_ok=False)
    scoped = configparser.RawConfigParser()
    scoped[section] = {key: selected[key] for key in PROFILE_KEYS if key in selected}
    if session_section:
        scoped[session_section] = {key: config[session_section][key] for key in SESSION_KEYS
                                  if key in config[session_section]}
    with (destination / "config").open("x", encoding="utf-8") as output:
        scoped.write(output)
    os.chmod(destination / "config", 0o600)
    cache = destination / "sso" / "cache"
    cache.mkdir(mode=0o700, parents=True)
    os.chmod(cache.parent, 0o700)
    target = cache / cache_name
    with target.open("xb") as output:
        output.write(token_bytes)
    os.chmod(target, 0o600)


def main() -> None:
    if os.environ.get("ADVERTIFIED_AGENT_RUNTIME_MODE") != "bedrock":
        raise RuntimeError("AWS bootstrap is restricted to explicitly enabled local preview.")
    os.umask(0o077)
    stage_profile(Path("/run/advertified-aws"), Path("/tmp/.aws"), os.environ["AWS_PROFILE"])
    os.execvp("python", ["python", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"])


if __name__ == "__main__":
    main()
