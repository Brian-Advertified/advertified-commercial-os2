"""Fail-closed inventory maintenance admission; independent of general runtime health."""

import os

from fastapi import HTTPException

PAUSE_KEY = "ADVERTIFIED_INVENTORY_PROCESSING_PAUSED"


def ensure_inventory_processing() -> None:
    if os.environ.get(PAUSE_KEY, "true").strip().lower() != "false":
        raise HTTPException(503, "Inventory processing is paused; retained work is unchanged.")
