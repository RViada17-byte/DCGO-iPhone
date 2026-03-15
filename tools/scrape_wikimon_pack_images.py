#!/usr/bin/env python3
from __future__ import annotations

import argparse
import io
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Iterable

try:
    from PIL import Image
except Exception:  # noqa: BLE001
    Image = None  # type: ignore[assignment]

ROOT_DIR = Path(__file__).resolve().parents[1]
WIKIMON_HOST = "https://wikimon.net"
WIKIMON_FILE_URL = f"{WIKIMON_HOST}/File:{{filename}}"
HEADERS = {
    "User-Agent": "DCGO Pack Image Scraper/1.0 (+https://github.com)",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
}

SET_PATTERN = re.compile(r"^(BT|EX)\s*[-]?\s*(\d+)$", re.IGNORECASE)
IMAGE_LINK_PATTERN = re.compile(
    r'<div class="fullImageLink".*?<a href="(?P<url>[^"]+)"',
    re.IGNORECASE | re.DOTALL,
)
OG_IMAGE_PATTERN = re.compile(
    r'<meta property="og:image" content="(?P<url>[^"]+)"',
    re.IGNORECASE,
)
MISSING_IMAGE_MARKERS: tuple[str, ...] = ()
PNG_EXTENSION = ".png"


def resolve_assets_dir(root_dir: Path) -> Path:
    for name in ("Assets", "assets"):
        path = root_dir / name
        if path.exists():
            return path
    raise FileNotFoundError("Could not find Assets/ (or assets/) directory.")


def read_local_sets(prefixes: Iterable[str]) -> dict[str, list[int]]:
    assets_dir = resolve_assets_dir(ROOT_DIR)
    cardbase_dir = assets_dir / "CardBaseEntity"
    if not cardbase_dir.is_dir():
        return {}

    discovered: dict[str, set[int]] = {prefix: set() for prefix in prefixes}
    pattern_cache: dict[str, re.Pattern[str]] = {
        prefix: re.compile(rf"^{re.escape(prefix)}(\d+)$", re.IGNORECASE)
        for prefix in prefixes
    }

    for entry in cardbase_dir.iterdir():
        if not entry.is_dir():
            continue
        match = None
        for prefix, pattern in pattern_cache.items():
            match = pattern.match(entry.name)
            if match:
                discovered[prefix].add(int(match.group(1)))
                break
        if match:
            continue

    return {prefix: sorted(values) for prefix, values in discovered.items() if values}


def parse_set_ids(raw: str) -> list[str]:
    ids: list[str] = []
    seen: set[str] = set()
    for token in raw.split(","):
        clean = token.strip()
        if not clean:
            continue
        match = SET_PATTERN.match(clean.upper().replace(" ", ""))
        if not match:
            print(f"warn: skipping unknown set id '{token.strip()}'")
            continue
        prefix, number = match.group(1), int(match.group(2))
        normalized = f"{prefix}-{number:02d}"
        if normalized in seen:
            continue
        seen.add(normalized)
        ids.append(normalized)
    return ids


def build_candidate_filenames(set_code: str) -> list[str]:
    match = SET_PATTERN.match(set_code.replace(" ", ""))
    if not match:
        return []

    prefix, number_text = match.group(1), match.group(2)
    number = int(number_text)
    base = f"{prefix}-{number:02d}"
    base_no_padding = f"{prefix}-{number}"
    compact = f"{prefix}{number}"
    compact_no_padding = f"{prefix}{number:02d}"

    variants = [
        base,
        base_no_padding,
        compact,
        compact_no_padding,
    ]
    candidates: list[str] = []
    seen = set()
    extensions = (".jpg", ".png", ".gif", ".jpeg")
    for variant in variants:
        for extension in extensions:
            filename = f"{variant}{extension}"
            if filename not in seen:
                seen.add(filename)
                candidates.append(filename)
    return candidates


def normalize_page_url(filename: str) -> str:
    return WIKIMON_FILE_URL.format(filename=urllib.parse.quote(filename))


def fetch_text(url: str, timeout_seconds: float) -> str:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout_seconds) as response:
        if response.status >= 400:
            raise urllib.error.HTTPError(url, response.status, response.reason, response.headers, None)
        return response.read().decode("utf-8", errors="ignore")


def extract_image_url(page_html: str) -> str:
    match = IMAGE_LINK_PATTERN.search(page_html)
    if match:
        return match.group("url")

    match = OG_IMAGE_PATTERN.search(page_html)
    if match:
        return match.group("url")

    return ""


def resolve_pack_image_url(set_code: str, timeout_seconds: float) -> str:
    for filename in build_candidate_filenames(set_code):
        page_url = normalize_page_url(filename)
        try:
            html = fetch_text(page_url, timeout_seconds=timeout_seconds)
        except urllib.error.HTTPError:
            continue
        except Exception:
            continue

        image_url = extract_image_url(html)
        if image_url:
            if image_url.startswith("//"):
                return f"https:{image_url}"
            if image_url.startswith("/"):
                return f"{WIKIMON_HOST}{image_url}"
            return image_url
    return ""


def decode_to_png_bytes(image_bytes: bytes) -> bytes:
    if Image is None:
        raise RuntimeError("Pillow is required to save images as PNG. Install Pillow and retry.")

    with Image.open(io.BytesIO(image_bytes)) as image:
        with io.BytesIO() as output:
            image.save(output, format="PNG")
            return output.getvalue()


def download_pack_image(set_code: str, output_path: Path, timeout_seconds: float, overwrite: bool) -> tuple[bool, str]:
    image_url = resolve_pack_image_url(set_code, timeout_seconds)
    if not image_url:
        return False, "not-found"

    req = urllib.request.Request(image_url, headers=HEADERS)
    try:
        with urllib.request.urlopen(req, timeout=timeout_seconds) as response:
            image_bytes = response.read()
    except urllib.error.HTTPError as exc:
        return False, f"http-{exc.code}"
    except Exception as exc:  # noqa: BLE001
        return False, f"error-{type(exc).__name__}"

    if not image_bytes:
        return False, "empty-response"

    try:
        png_bytes = decode_to_png_bytes(image_bytes)
    except Exception as exc:  # noqa: BLE001
        return False, f"convert-{type(exc).__name__}"

    target = output_path / f"{set_code}{PNG_EXTENSION}"
    if target.exists() and not overwrite:
        return False, "exists"

    output_path.mkdir(parents=True, exist_ok=True)
    target.write_bytes(png_bytes)
    return True, str(target)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Scrape Digimon pack images from Wikimon.")
    parser.add_argument(
        "--sets",
        default="",
        help="Optional comma-separated set ids, e.g. BT-01,EX-03",
    )
    parser.add_argument(
        "--bt-start",
        type=int,
        default=None,
        help="Start of BT range to query when local set scan is unavailable.",
    )
    parser.add_argument(
        "--bt-end",
        type=int,
        default=None,
        help="End of BT range to query when local set scan is unavailable.",
    )
    parser.add_argument(
        "--ex-start",
        type=int,
        default=None,
        help="Start of EX range to query when local set scan is unavailable.",
    )
    parser.add_argument(
        "--ex-end",
        type=int,
        default=None,
        help="End of EX range to query when local set scan is unavailable.",
    )
    parser.add_argument(
        "--output-dir",
        default=str(ROOT_DIR / "Builds" / "wikimon_packs"),
        help="Directory for downloaded cover images.",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Overwrite images already present in output directory.",
    )
    parser.add_argument("--timeout", type=float, default=20.0, help="Per-request timeout in seconds.")
    parser.add_argument("--sleep", type=float, default=0.0, help="Pause between downloads in seconds.")
    parser.add_argument("--dry-run", action="store_true", help="Only print what would be downloaded.")
    parser.add_argument("--skip-existing", action="store_true", help="Skip already-downloaded files.")
    return parser.parse_args()


def build_target_set_ids(args: argparse.Namespace) -> list[str]:
    if args.sets:
        parsed = parse_set_ids(args.sets)
        if parsed:
            return parsed

    local_sets = read_local_sets(["BT", "EX"])
    output: list[str] = []
    if "BT" in local_sets:
        output.extend(f"BT-{num:02d}" for num in local_sets["BT"])
    if "EX" in local_sets:
        output.extend(f"EX-{num:02d}" for num in local_sets["EX"])

    if not output:
        bt_range = (args.bt_start or 1, args.bt_end or 24)
        ex_range = (args.ex_start or 1, args.ex_end or 11)
        output.extend(f"BT-{num:02d}" for num in range(bt_range[0], bt_range[1] + 1))
        output.extend(f"EX-{num:02d}" for num in range(ex_range[0], ex_range[1] + 1))
    else:
        if args.bt_start is not None or args.bt_end is not None:
            output = [set_id for set_id in output if not set_id.startswith("BT-") or (
                (args.bt_start is None or int(set_id[3:]) >= args.bt_start)
                and (args.bt_end is None or int(set_id[3:]) <= args.bt_end)
            )]
        if args.ex_start is not None or args.ex_end is not None:
            output = [set_id for set_id in output if not set_id.startswith("EX-") or (
                (args.ex_start is None or int(set_id[3:]) >= args.ex_start)
                and (args.ex_end is None or int(set_id[3:]) <= args.ex_end)
            )]

    return output


def main() -> int:
    args = parse_args()
    output_dir = Path(args.output_dir).expanduser().resolve()

    set_ids = build_target_set_ids(args)
    if not set_ids:
        print("No BT/EX set ids to process.")
        return 1

    print(f"target={len(set_ids)} output_dir={output_dir}")

    downloaded = 0
    skipped = 0
    failed: list[tuple[str, str]] = []

    for index, set_code in enumerate(set_ids, start=1):
        if args.skip_existing:
            existing = output_dir / f"{set_code}{PNG_EXTENSION}"
            if existing.exists():
                skipped += 1
                print(f"[{index:02d}/{len(set_ids)}] SKIP existing {set_code} -> {existing.name}")
                continue

        if args.dry_run:
            print(f"[{index:02d}/{len(set_ids)}] DRY {set_code}")
            continue

        ok, detail = download_pack_image(
            set_code=set_code,
            output_path=output_dir,
            timeout_seconds=args.timeout,
            overwrite=args.overwrite,
        )
        if ok:
            downloaded += 1
            print(f"[{index:02d}/{len(set_ids)}] OK   {detail}")
        elif detail == "exists":
            skipped += 1
            print(f"[{index:02d}/{len(set_ids)}] SKIP existing {set_code}")
        else:
            failed.append((set_code, detail))
            print(f"[{index:02d}/{len(set_ids)}] FAIL {set_code} ({detail})")

        if args.sleep > 0:
            time.sleep(args.sleep)

    print(f"summary downloaded={downloaded} skipped={skipped} failed={len(failed)}")
    if failed:
        failures_path = ROOT_DIR / "Logs" / "wikimon_pack_download_failures.txt"
        failures_path.parent.mkdir(parents=True, exist_ok=True)
        failures_path.write_text(
            "\n".join(f"{set_id}\t{status}" for set_id, status in failed),
            encoding="utf-8",
        )
        print(f"failure_log={failures_path}")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
