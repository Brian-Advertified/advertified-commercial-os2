"""Generate a stable alias to the governed PendingSupplier pricing code."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (
    ROOT / "api" / "src" / "Advertified.Commercial.Domain"
    / "Generated" / "MasterDataCodes.g.cs"
)
OUTPUT = (
    ROOT / "api" / "src" / "Advertified.Commercial.Infrastructure"
    / "Inventory" / "InventoryPricingCodes.g.cs"
)


def main() -> int:
    source = SOURCE.resolve(strict=True).read_text(encoding="utf-8")
    match = re.search(
        r"public static class (?P<class>\w+)\s*\{(?:(?!public static class).)*?"
        r"public const string PendingSupplier\s*=\s*\"PENDING_SUPPLIER\";",
        source,
        flags=re.DOTALL,
    )
    if not match:
        raise RuntimeError(
            "The governed PENDING_SUPPLIER code was not found in generated master data."
        )
    class_name = match.group("class")
    OUTPUT.write_text(
        "using Advertified.Commercial.Domain.MasterData;\n\n"
        "namespace Advertified.Commercial.Infrastructure.Inventory;\n\n"
        "internal static class InventoryPricingCodes\n"
        "{\n"
        "    internal const string PendingSupplier =\n"
        f"        MasterDataCodes.{class_name}.PendingSupplier;\n"
        "}\n",
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
