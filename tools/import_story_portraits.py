#!/usr/bin/env python3
from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

import requests
from PIL import Image


ROOT_DIR = Path(__file__).resolve().parents[1]
USER_AGENT = "DCGO Story Portrait Importer/1.0"
MAX_DIMENSION = 1024


def resolve_assets_dir(root_dir: Path) -> Path:
    for name in ("assets", "Assets"):
        candidate = root_dir / name
        if candidate.exists():
            return candidate
    raise FileNotFoundError("Could not locate Unity assets directory.")


ASSETS_DIR = resolve_assets_dir(ROOT_DIR)
DEFAULT_MANIFEST_PATH = ASSETS_DIR / "StreamingAssets" / "story_portraits.json"
DEFAULT_OUTPUT_DIR = ASSETS_DIR / "StreamingAssets" / "Textures" / "StoryPortraits"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Import story dialogue portraits from a local manifest.")
    parser.add_argument(
        "--manifest",
        default=str(DEFAULT_MANIFEST_PATH),
        help="Path to the portrait manifest JSON.",
    )
    parser.add_argument(
        "--output-dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help="Directory where normalized portrait PNGs should be written.",
    )
    parser.add_argument(
        "--ids",
        default="",
        help="Optional comma-separated list of portrait ids to import.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Re-download portraits even if the normalized PNG already exists.",
    )
    return parser.parse_args()


def load_manifest(path: Path) -> list[dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    portraits = data.get("portraits")
    if not isinstance(portraits, list):
        raise ValueError(f"Manifest at {path} does not contain a 'portraits' array.")
    return portraits


def normalize_image(raw_bytes: bytes) -> Image.Image:
    with Image.open(io.BytesIO(raw_bytes)) as source:
        source.seek(0)
        image = source.convert("RGBA")

    alpha_box = image.getchannel("A").getbbox()
    if alpha_box is not None:
        image = image.crop(alpha_box)

    width, height = image.size
    if width <= 0 or height <= 0:
        raise ValueError("Source image has invalid dimensions.")

    longest_edge = max(width, height)
    if longest_edge > MAX_DIMENSION:
        scale = MAX_DIMENSION / float(longest_edge)
        resized = (
            max(1, int(round(width * scale))),
            max(1, int(round(height * scale))),
        )
        image = image.resize(resized, Image.LANCZOS)

    return image


def download_image(url: str) -> bytes:
    response = requests.get(
        url,
        timeout=60,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "image/avif,image/webp,image/apng,image/*,*/*;q=0.8",
        },
    )
    response.raise_for_status()
    return response.content


def import_portrait(entry: dict, output_dir: Path, force: bool) -> tuple[str, str]:
    portrait_id = str(entry.get("id", "")).strip()
    source_url = str(entry.get("sourceImageUrl", "")).strip()
    if not portrait_id:
        raise ValueError("Portrait manifest entry is missing 'id'.")
    if not source_url:
        raise ValueError(f"Portrait '{portrait_id}' is missing 'sourceImageUrl'.")

    output_path = output_dir / f"{portrait_id}.png"
    if output_path.exists() and not force:
        return portrait_id, "skipped"

    raw_bytes = download_image(source_url)
    image = normalize_image(raw_bytes)
    output_dir.mkdir(parents=True, exist_ok=True)
    image.save(output_path, format="PNG")
    return portrait_id, "imported"


def main() -> int:
    args = parse_args()
    manifest_path = Path(args.manifest).expanduser().resolve()
    output_dir = Path(args.output_dir).expanduser().resolve()

    portraits = load_manifest(manifest_path)
    requested_ids = {
        value.strip()
        for value in args.ids.split(",")
        if value.strip()
    }

    if requested_ids:
        portraits = [entry for entry in portraits if entry.get("id") in requested_ids]

    if not portraits:
        print("No portraits selected.")
        return 0

    imported = 0
    skipped = 0
    failed = 0

    for entry in portraits:
        portrait_id = entry.get("id", "<unknown>")
        try:
            _, status = import_portrait(entry, output_dir, force=args.force)
        except Exception as exc:  # noqa: BLE001
            failed += 1
            print(f"FAILED  {portrait_id}: {exc}")
            continue

        if status == "imported":
            imported += 1
            print(f"IMPORTED {portrait_id}")
        else:
            skipped += 1
            print(f"SKIPPED  {portrait_id}")

    print(f"Summary: imported={imported} skipped={skipped} failed={failed}")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
