#!/usr/bin/env python3
from __future__ import annotations

import argparse
import concurrent.futures
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[1]
CARD_ENTITY_DIR = ROOT_DIR / "Assets" / "CardBaseEntity"
CARD_ART_DIR = ROOT_DIR / "Assets" / "StreamingAssets" / "Textures" / "Card"
CARD_SPRITE_PATTERN = re.compile(r"^  CardSpriteName: (.+)$", re.MULTILINE)
ERRATA_SUFFIX_PATTERN = re.compile(r"([_-])ERRATA$", re.IGNORECASE)
EXCLUDED_CARD_CODES = {
    "LM-051",
    "LM-052",
    "LM-053",
    "LM-054",
    "LM-055",
    "LM-056",
}


def is_allowed_set(set_id: str) -> bool:
    normalized = (set_id or "").strip().upper()
    if not normalized:
        return False

    if normalized.startswith("AD"):
        return False

    if normalized in {"LM", "P", "RB1"}:
        return True

    match = re.match(r"^([A-Z]+)(\d+)$", normalized)
    if not match:
        return False

    prefix = match.group(1)
    value = int(match.group(2))

    if prefix == "BT":
        return 1 <= value <= 22

    if prefix == "EX":
        return 1 <= value <= 10

    if prefix == "ST":
        return 1 <= value <= 22

    return False


def collect_required_sprite_names(include_parallels: bool, include_tokens: bool) -> list[str]:
    sprite_names: set[str] = set()

    for asset_path in CARD_ENTITY_DIR.rglob("*.asset"):
        try:
            text = asset_path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue

        match = CARD_SPRITE_PATTERN.search(text)
        if not match:
            continue

        sprite_name = match.group(1).strip()
        normalized_name = sprite_name.upper()
        normalized_name_without_errata = ERRATA_SUFFIX_PATTERN.sub("", normalized_name)
        set_id = normalized_name.replace("_", "-").split("-", 1)[0]

        if not is_allowed_set(set_id):
            continue

        if not include_tokens and "TOKEN" in normalized_name:
            continue

        if not include_parallels and re.search(r"([_-])P\d+$", normalized_name_without_errata):
            continue

        normalized_download_name = normalize_download_name(sprite_name)
        if normalized_download_name.upper() in EXCLUDED_CARD_CODES:
            continue

        sprite_names.add(normalized_download_name)

    return sorted(sprite_names, key=lambda value: value.upper())


def normalize_download_name(sprite_name: str) -> str:
    normalized = (sprite_name or "").strip()
    return ERRATA_SUFFIX_PATTERN.sub("", normalized)


def collect_existing_stems() -> set[str]:
    existing: set[str] = set()

    if not CARD_ART_DIR.exists():
        return existing

    for path in CARD_ART_DIR.iterdir():
        if path.is_file() and path.suffix.lower() in {".png", ".jpg", ".webp"}:
            existing.add(path.stem.upper())

    return existing


def download_sprite(sprite_name: str, timeout_seconds: float) -> tuple[str, str]:
    url = f"https://world.digimoncard.com/images/cardlist/card/{sprite_name}.png"
    target_path = CARD_ART_DIR / f"{sprite_name}.png"
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "DCGO Card Art Sync/1.0",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            data = response.read()
    except urllib.error.HTTPError as exc:
        return sprite_name, f"http_{exc.code}"
    except Exception as exc:  # noqa: BLE001
        return sprite_name, f"error_{type(exc).__name__}"

    try:
        target_path.write_bytes(data)
    except OSError as exc:
        return sprite_name, f"write_error_{type(exc).__name__}"

    return sprite_name, "ok"


def main() -> int:
    parser = argparse.ArgumentParser(description="Download missing DCGO card art from the official English site.")
    parser.add_argument("--include-parallels", action="store_true", help="Download parallel art as well as base art.")
    parser.add_argument("--include-tokens", action="store_true", help="Download token art too.")
    parser.add_argument("--limit", type=int, default=0, help="Cap the number of missing images processed.")
    parser.add_argument("--workers", type=int, default=12, help="Concurrent download workers.")
    parser.add_argument("--timeout", type=float, default=20.0, help="Per-request timeout in seconds.")
    parser.add_argument("--sleep", type=float, default=0.0, help="Optional pause between completed downloads.")
    parser.add_argument("--dry-run", action="store_true", help="Only report the missing set; do not download.")
    args = parser.parse_args()

    CARD_ART_DIR.mkdir(parents=True, exist_ok=True)

    required = collect_required_sprite_names(
        include_parallels=args.include_parallels,
        include_tokens=args.include_tokens,
    )
    existing = collect_existing_stems()
    missing = [name for name in required if name.upper() not in existing]

    if args.limit > 0:
        missing = missing[:args.limit]

    print(f"required={len(required)} existing={len(required) - len(missing)} missing={len(missing)}")

    if missing:
        preview = ", ".join(missing[:20])
        print(f"missing_sample={preview}")

    if args.dry_run or not missing:
        return 0

    completed = 0
    ok_count = 0
    failed: list[tuple[str, str]] = []

    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.workers)) as executor:
        future_map = {
            executor.submit(download_sprite, sprite_name, args.timeout): sprite_name
            for sprite_name in missing
        }

        for future in concurrent.futures.as_completed(future_map):
            sprite_name = future_map[future]
            try:
                _, status = future.result()
            except Exception as exc:  # noqa: BLE001
                status = f"error_{type(exc).__name__}"

            completed += 1

            if status == "ok":
                ok_count += 1
            else:
                failed.append((sprite_name, status))

            if completed % 25 == 0 or completed == len(missing):
                print(f"progress={completed}/{len(missing)} ok={ok_count} failed={len(failed)}")

            if args.sleep > 0:
                time.sleep(args.sleep)

    if failed:
        failures_path = ROOT_DIR / "Logs" / "card_art_download_failures.txt"
        failures_path.parent.mkdir(parents=True, exist_ok=True)
        failures_path.write_text(
            "\n".join(f"{sprite_name}\t{status}" for sprite_name, status in failed),
            encoding="utf-8",
        )
        print(f"failure_log={failures_path}")

    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
