#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_DIR_DEFAULT="$(cd "$ROOT_DIR/../.." && pwd)/Digimon Card Tag Force Project/data/external/dcgo/images"
SOURCE_DIR="${1:-${DCGO_CARD_ART_SOURCE:-$SOURCE_DIR_DEFAULT}}"
SET_FILTER="${2:-${DCGO_CARD_ART_SETS:-BT1,BT2,ST1,ST2}}"
CLEAN_TARGET="${3:-${DCGO_CARD_ART_CLEAN:-1}}"
TARGET_DIR="$ROOT_DIR/Assets/StreamingAssets/Textures/Card"

if [[ ! -d "$SOURCE_DIR" ]]; then
    echo "Card art source directory not found: $SOURCE_DIR" >&2
    echo "Pass source path as first arg or set DCGO_CARD_ART_SOURCE." >&2
    exit 1
fi

mkdir -p "$TARGET_DIR"

if [[ "$CLEAN_TARGET" == "1" ]]; then
    find "$TARGET_DIR" -maxdepth 1 -type f \
        \( -iname '*.png' -o -iname '*.jpg' -o -iname '*.webp' -o -iname '*.png.meta' -o -iname '*.jpg.meta' -o -iname '*.webp.meta' \) \
        -delete
fi

IFS=',' read -r -a SET_PREFIXES <<< "$SET_FILTER"

copied_count=0
converted_count=0
matched_count=0

echo "Syncing card art"
echo "  source: $SOURCE_DIR"
echo "  target: $TARGET_DIR"
echo "  sets:   $SET_FILTER"
echo "  clean:  $CLEAN_TARGET"

collect_matches_for_prefix() {
    local prefix="$1"
    if [[ "$prefix" == "ALL" ]]; then
        find "$SOURCE_DIR" -maxdepth 1 -type f \
            \( -iname '*.webp' -o -iname '*.png' -o -iname '*.jpg' \) \
            -print0 | sort -z
    else
        find "$SOURCE_DIR" -maxdepth 1 -type f \
            \( -iname "${prefix}-*.webp" -o -iname "${prefix}-*.png" -o -iname "${prefix}-*.jpg" \) \
            -print0 | sort -z
    fi
}

for raw_prefix in "${SET_PREFIXES[@]}"; do
    prefix="$(echo "$raw_prefix" | xargs)"
    if [[ -z "$prefix" ]]; then
        continue
    fi

    while IFS= read -r -d '' src; do
        ((matched_count += 1))
        base_name="$(basename "$src")"
        stem="${base_name%.*}"
        ext="${base_name##*.}"
        ext_lower="$(echo "$ext" | tr '[:upper:]' '[:lower:]')"

        if [[ "$ext_lower" == "webp" ]]; then
            output_path="$TARGET_DIR/${stem}.jpg"
            sips -s format jpeg "$src" --out "$output_path" >/dev/null
            ((converted_count += 1))
        else
            cp "$src" "$TARGET_DIR/$base_name"
            ((copied_count += 1))
        fi
    done < <(collect_matches_for_prefix "$prefix")
done

target_count="$(find "$TARGET_DIR" -maxdepth 1 -type f \( -iname '*.png' -o -iname '*.jpg' -o -iname '*.webp' \) | wc -l | tr -d ' ')"
target_size="$(du -sh "$TARGET_DIR" | awk '{print $1}')"

echo "Sync complete: matched=$matched_count converted=$converted_count copied=$copied_count target_files=$target_count target_size=$target_size"
