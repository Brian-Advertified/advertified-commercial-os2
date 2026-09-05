"""Coordinate independent enumeration for the remaining 15 dense sources."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Callable

from inventory_reference_dense_broadcast import (
    BROADCAST_HASHES,
    broadcast_reference_entries,
)
from inventory_reference_dense_rates import RATE_HASHES, dense_rate_entries
from inventory_reference_dense_sites import SITE_HASHES, site_reference_entries


def dense_reference_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_maps: Path,
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for source_hash in sorted(SITE_HASHES | BROADCAST_HASHES | RATE_HASHES):
        source = load_source_map(source_maps, source_hash)
        if source_hash in SITE_HASHES:
            result.extend(site_reference_entries(make_entry, source_hash, source))
        elif source_hash in BROADCAST_HASHES:
            result.extend(broadcast_reference_entries(make_entry, source_hash, source))
        else:
            result.extend(dense_rate_entries(make_entry, source_hash, source))
    identifiers = [item["entry_id"] for item in result]
    if len(identifiers) != len(set(identifiers)):
        raise ValueError("Dense reference enumeration produced duplicate entry ids.")
    return result


def load_source_map(source_maps: Path, source_hash: str) -> dict[str, Any]:
    path = source_maps / f"{source_hash}.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("sourceHash") != source_hash:
        raise ValueError(f"Source-map hash mismatch: {path}")
    return payload
