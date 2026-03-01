#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_DIR_DEFAULT="$(cd "$ROOT_DIR/.." && pwd)/DCGO-Card-Scripts/CardEffect"
SOURCE_DIR="${1:-${DCGO_CARD_SCRIPTS_DIR:-$SOURCE_DIR_DEFAULT}}"
TARGET_DIR="$ROOT_DIR/Assets/Scripts/CardEffect"

if [[ ! -d "$SOURCE_DIR" ]]; then
    echo "Card script source directory not found: $SOURCE_DIR" >&2
    echo "Pass source path as first arg or set DCGO_CARD_SCRIPTS_DIR." >&2
    exit 1
fi

if [[ ! -d "$TARGET_DIR" ]]; then
    echo "Target card script directory not found: $TARGET_DIR" >&2
    exit 1
fi

echo "Syncing card scripts"
echo "  source: $SOURCE_DIR"
echo "  target: $TARGET_DIR"

# Keep Unity .meta files managed in-project; sync only C# sources.
rsync -a --delete \
    --include='*/' \
    --include='*.cs' \
    --exclude='*' \
    "$SOURCE_DIR/" "$TARGET_DIR/"

SOURCE_COUNT="$(find "$SOURCE_DIR" -type f -name '*.cs' | wc -l | tr -d ' ')"
TARGET_COUNT="$(find "$TARGET_DIR" -type f -name '*.cs' | wc -l | tr -d ' ')"

echo "Sync complete: source_cs=$SOURCE_COUNT target_cs=$TARGET_COUNT"
