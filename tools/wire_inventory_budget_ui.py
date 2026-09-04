from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
path = root / "web" / "src" / "inventory" / "InventorySemanticPreflightPanel.tsx"
text = path.read_text(encoding="utf-8")
import_line = (
    'import { InventoryBedrockBudgetSummary } from '
    '"./InventoryBedrockBudgetSummary";\n'
)
if import_line not in text:
    imports = list(re.finditer(r"^import .*?;\s*$", text, re.MULTILINE))
    if not imports:
        raise RuntimeError("Could not locate imports in the preflight panel.")
    insert_at = imports[-1].end()
    text = text[:insert_at] + "\n" + import_line.rstrip() + text[insert_at:]

match = re.search(r"\b([A-Za-z_$][\w$]*)\.existingCommittedCostUsdMicros\b", text)
if not match:
    raise RuntimeError("Could not identify the preflight value variable.")
variable = match.group(1)
paragraph = re.compile(
    r"<p(?P<attrs>[^>]*)>\s*Paid inventory intelligence is governed by the US\$5 corpus certification budget\. This panel shows actual spend, reserved exposure, remaining budget, and the reason for every provider call\.\s*</p>",
    re.MULTILINE,
)
replacement = f"<InventoryBedrockBudgetSummary preflight={{{variable}}} />"
if paragraph.search(text):
    text = paragraph.sub(replacement, text, count=1)
elif replacement not in text:
    sentence = (
        "Paid inventory intelligence is governed by the US$5 corpus "
        "certification budget. This panel shows actual spend, reserved "
        "exposure, remaining budget, and the reason for every provider call."
    )
    if sentence not in text:
        raise RuntimeError("Could not locate the budget explanation anchor.")
    text = text.replace(sentence, replacement, 1)

path.write_text(text, encoding="utf-8")
