"""Render hash-bound inventory pages/slides and compact contact sheets for inspection."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


SUPPORTED = {"pdf", "powerpoint"}
DEFAULT_SOFFICE = Path(r"C:\Program Files\LibreOffice\program\soffice.exe")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run(command: list[str], timeout: int = 900) -> None:
    subprocess.run(
        command,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        timeout=timeout,
    )


def convert_presentation(source: Path, target_dir: Path, soffice: Path) -> Path:
    target_dir.mkdir(parents=True, exist_ok=True)
    target = target_dir / f"{source.stem}.pdf"
    if target.exists() and target.stat().st_mtime_ns >= source.stat().st_mtime_ns:
        return target
    with tempfile.TemporaryDirectory(prefix="advertified-lo-") as profile:
        profile_uri = Path(profile).resolve().as_uri()
        run([
            str(soffice),
            "--headless",
            f"-env:UserInstallation={profile_uri}",
            "--convert-to",
            "pdf",
            "--outdir",
            str(target_dir),
            str(source),
        ])
    if not target.exists():
        raise RuntimeError("LibreOffice did not create the expected PDF.")
    return target


def render_pdf(source: Path, page_dir: Path, pdftoppm: str, dpi: int) -> list[Path]:
    page_dir.mkdir(parents=True, exist_ok=True)
    complete = page_dir / ".complete"
    pages = sorted(page_dir.glob("unit-*.jpg"))
    if complete.exists() and pages:
        return pages
    for stale in pages:
        stale.unlink()
    run([
        pdftoppm,
        "-jpeg",
        "-r",
        str(dpi),
        "-jpegopt",
        "quality=90,optimize=y",
        str(source),
        str(page_dir / "unit"),
    ])
    pages = sorted(page_dir.glob("unit-*.jpg"))
    if not pages:
        raise RuntimeError("pdftoppm produced no pages.")
    complete.write_text(str(len(pages)), encoding="utf-8")
    return pages


def fit(image: Image.Image, width: int, height: int) -> Image.Image:
    copy = image.copy()
    copy.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), "white")
    left = (width - copy.width) // 2
    top = (height - copy.height) // 2
    canvas.paste(copy, (left, top))
    return canvas


def contact_sheets(
    pages: list[Path], target_dir: Path, source_label: str, cells: int = 4
) -> list[Path]:
    target_dir.mkdir(parents=True, exist_ok=True)
    outputs: list[Path] = []
    cell_width, cell_height, header = 1240, 1760, 54
    columns = 2
    rows = max(1, (cells + columns - 1) // columns)
    font = ImageFont.load_default(size=24)
    for batch_start in range(0, len(pages), cells):
        batch = pages[batch_start : batch_start + cells]
        output = target_dir / f"contact-{batch_start + 1:04d}-{batch_start + len(batch):04d}.jpg"
        if output.exists():
            outputs.append(output)
            continue
        sheet = Image.new("RGB", (columns * cell_width, rows * (cell_height + header)), "#d1d5db")
        draw = ImageDraw.Draw(sheet)
        for offset, page in enumerate(batch):
            with Image.open(page) as original:
                rendered = fit(original.convert("RGB"), cell_width - 8, cell_height - 8)
            x = (offset % columns) * cell_width + 4
            y = (offset // columns) * (cell_height + header) + header
            sheet.paste(rendered, (x, y))
            label = f"{source_label} | unit {batch_start + offset + 1}"
            draw.text((x + 8, y - header + 12), label, fill="black", font=font)
        sheet.save(output, "JPEG", quality=88, optimize=True)
        outputs.append(output)
    return outputs


def render_entry(entry: dict, output_root: Path, pdftoppm: str, soffice: Path, dpi: int) -> dict:
    source = Path(entry["absolute_path"])
    expected_hash = entry["content_hash"]
    actual_hash = sha256(source)
    if actual_hash != expected_hash:
        raise ValueError("Source hash no longer matches the manifest.")
    unit_root = output_root / "units" / actual_hash
    source_for_render = source
    if entry["format"] == "powerpoint":
        source_for_render = convert_presentation(source, output_root / "converted", soffice)
    pages = render_pdf(source_for_render, unit_root, pdftoppm, dpi)
    contacts = contact_sheets(
        pages,
        output_root / "contacts" / actual_hash,
        source.name,
    )
    expected_units = entry.get("page_count") or entry.get("slide_count")
    return {
        "source_hash": actual_hash,
        "file_name": source.name,
        "format": entry["format"],
        "expected_units": expected_units,
        "rendered_units": len(pages),
        "unit_paths": [str(path.resolve()) for path in pages],
        "contact_paths": [str(path.resolve()) for path in contacts],
        "status": "RENDERED" if expected_units == len(pages) else "COUNT_MISMATCH",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dpi", type=int, default=150)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    pdftoppm = shutil.which("pdftoppm")
    soffice = DEFAULT_SOFFICE
    if not pdftoppm or not soffice.exists():
        raise RuntimeError("Required PDF or presentation renderer is unavailable.")
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    for index, entry in enumerate(manifest["files"], start=1):
        if entry["format"] not in SUPPORTED:
            continue
        print(f"[{index}/{len(manifest['files'])}] {entry['filename']}", flush=True)
        try:
            results.append(render_entry(entry, args.output, pdftoppm, soffice, args.dpi))
        except Exception as error:  # preserve per-file progress and exact blocker
            results.append({
                "source_hash": entry["content_hash"],
                "file_name": entry["filename"],
                "format": entry["format"],
                "status": "BLOCKED",
                "error": f"{type(error).__name__}: {error}",
            })
        report = {
            "schema_version": "advertified.inventory-visual-render.v1",
            "generated_at_utc": datetime.now(timezone.utc).isoformat(),
            "source_manifest": str(args.manifest.resolve()),
            "dpi": args.dpi,
            "files": results,
        }
        (args.output / "render_manifest.json").write_text(
            json.dumps(report, indent=2), encoding="utf-8"
        )
    return 0 if all(item["status"] == "RENDERED" for item in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
