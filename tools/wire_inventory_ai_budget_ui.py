"""Wire the governed inventory-AI budget card into the preflight UI."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = (
    ROOT / "web" / "src" / "inventory"
    / "InventorySemanticPreflightPanel.tsx"
)
IMPORT = "import { InventoryAiBudgetSummary } from './InventoryAiBudgetSummary'"
MARKER = "<InventoryAiBudgetSummary"
COPY = (
    "Inventory AI has a hard US$5.00 corpus ceiling. "
    "US$0.188122 is already budget-accounted: US$0.090935 confirmed "
    "historical certification usage plus a US$0.097187 reserve for failed "
    "calls with incomplete usage evidence. The live usage below records each "
    "new request, why it was made, its actual cost and its maximum commitment; "
    "processing stops before the remaining US$4.811878 can be exceeded."
)


def main() -> int:
    source = TARGET.resolve(strict=True).read_text(encoding="utf-8")
    if IMPORT not in source:
        imports = list(re.finditer(r"(?m)^import\s.+$", source))
        if not imports:
            raise RuntimeError("Preflight panel has no import section.")
        end = imports[-1].end()
        source = source[:end] + "\n" + IMPORT + source[end:]

    if MARKER in source:
        TARGET.write_text(source, encoding="utf-8")
        return 0

    expressions = re.findall(
        r"([A-Za-z_$][A-Za-z0-9_$]*(?:\.[A-Za-z_$][A-Za-z0-9_$]*)*)"
        r"\.existingCommittedCostUsdMicros",
        source,
    )
    if not expressions:
        expressions = re.findall(
            r"([A-Za-z_$][A-Za-z0-9_$]*(?:\.[A-Za-z_$][A-Za-z0-9_$]*)*)"
            r"\.committedCostUsdMicros",
            source,
        )
    if not expressions:
        raise RuntimeError(
            "The preflight panel does not expose a committed-cost expression."
        )
    expression = expressions[0]

    copy_index = source.find(COPY)
    if copy_index < 0:
        raise RuntimeError("The governed budget copy was not found.")
    close = source.find("</p>", copy_index)
    if close < 0:
        raise RuntimeError("The budget copy is not contained in a paragraph.")
    close += len("</p>")
    indentation_match = re.search(r"(?m)^(\s*)<p[^>]*>[^<]*" + re.escape(COPY[:24]), source)
    indentation = indentation_match.group(1) if indentation_match else "      "
    component = (
        "\n"
        + indentation
        + "<InventoryAiBudgetSummary\n"
        + indentation
        + "  activeCommittedUsdMicros={Number("
        + expression
        + ".existingCommittedCostUsdMicros ?? 0)}\n"
        + indentation
        + "/>"
    )
    source = source[:close] + component + source[close:]
    TARGET.write_text(source, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
