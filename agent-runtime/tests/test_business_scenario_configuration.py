"""Live certification selects the deployed model unless explicitly overridden."""

import os

import pytest

from business_scenarios.bedrock_support import DEFAULT_MODEL, configure_live_bedrock


@pytest.mark.parametrize("override,explicit,expected", [
    (None, None, DEFAULT_MODEL),
    ("amazon.nova-pro-v1:0", None, "amazon.nova-pro-v1:0"),
    ("amazon.nova-pro-v1:0", DEFAULT_MODEL, DEFAULT_MODEL),
])
def test_live_model_selection(monkeypatch, override, explicit, expected):
    # Restore every process setting mutated by configuration; no provider is called.
    monkeypatch.setattr(os, "environ", os.environ.copy())
    if override is None:
        monkeypatch.delenv("ADVERTIFIED_BUSINESS_SCENARIO_MODEL", raising=False)
    else:
        monkeypatch.setenv("ADVERTIFIED_BUSINESS_SCENARIO_MODEL", override)
    assert configure_live_bedrock(explicit) == expected
    assert os.environ["ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST"] == expected


def test_unpriced_model_override_fails_before_enabling_provider(monkeypatch):
    monkeypatch.setattr(os, "environ", os.environ.copy())
    monkeypatch.delenv("ADVERTIFIED_AGENT_RUNTIME_MODE", raising=False)
    monkeypatch.setenv("ADVERTIFIED_BUSINESS_SCENARIO_MODEL", "unpriced-model")
    with pytest.raises(ValueError, match="No governed"):
        configure_live_bedrock()
    assert "ADVERTIFIED_AGENT_RUNTIME_MODE" not in os.environ
