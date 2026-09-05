from unittest.mock import patch

import pytest
from fastapi import HTTPException

from inventory_processing_control import PAUSE_KEY, ensure_inventory_processing
from main import _execute_embedding, live


@pytest.mark.parametrize("value", [None, "true", "", "invalid", "0"])
def test_pause_defaults_closed_before_embedding_provider(value, monkeypatch):
    monkeypatch.delenv(PAUSE_KEY, raising=False)
    if value is not None:
        monkeypatch.setenv(PAUSE_KEY, value)
    with patch("main.bedrock_embedding", side_effect=AssertionError("No provider call")):
        with pytest.raises(HTTPException, match="paused") as error:
            _execute_embedding(None, "bedrock")
    assert error.value.status_code == 503
    assert live().status == "healthy"


def test_only_explicit_resume_opens_admission(monkeypatch):
    monkeypatch.setenv(PAUSE_KEY, "false")
    ensure_inventory_processing()
