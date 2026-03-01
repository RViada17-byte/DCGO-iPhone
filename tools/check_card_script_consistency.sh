#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_DIR_DEFAULT="$(cd "$ROOT_DIR/.." && pwd)/DCGO-Card-Scripts/CardEffect"
SOURCE_DIR="${1:-${DCGO_CARD_SCRIPTS_DIR:-$SOURCE_DIR_DEFAULT}}"
TARGET_DIR="$ROOT_DIR/Assets/Scripts/CardEffect"

if [[ ! -d "$SOURCE_DIR" ]]; then
    echo "Source card script directory not found: $SOURCE_DIR" >&2
    exit 1
fi

if [[ ! -d "$TARGET_DIR" ]]; then
    echo "Target card script directory not found: $TARGET_DIR" >&2
    exit 1
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

source_counts="$TMP_DIR/source_counts.txt"
target_counts="$TMP_DIR/target_counts.txt"

find "$SOURCE_DIR" -type f -name '*.cs' \
    | sed "s#^$SOURCE_DIR/##" \
    | awk -F/ '{print $1}' \
    | LC_ALL=C sort \
    | uniq -c \
    | awk '{print $2":"$1}' \
    | LC_ALL=C sort -t: -k1,1 > "$source_counts"

find "$TARGET_DIR" -type f -name '*.cs' \
    | sed "s#^$TARGET_DIR/##" \
    | awk -F/ '{print $1}' \
    | LC_ALL=C sort \
    | uniq -c \
    | awk '{print $2":"$1}' \
    | LC_ALL=C sort -t: -k1,1 > "$target_counts"

echo "Per-set card script counts (source vs target)"
LC_ALL=C join -a1 -a2 -e "0" -o '0,1.2,2.2' -t ':' "$source_counts" "$target_counts" \
    | awk -F: '{printf "  %-8s source=%-5s target=%-5s\n", $1, $2, $3}'

fail=0

if ! diff -u "$source_counts" "$target_counts" >/dev/null; then
    echo
    echo "Set-level count mismatch detected."
    diff -u "$source_counts" "$target_counts" || true
    fail=1
fi

echo
echo "Checking file/class naming consistency in target..."
while IFS= read -r file; do
    class_name="$(basename "$file" .cs)"
    if ! rg -q "class\\s+${class_name}\\b" "$file"; then
        echo "  Missing matching class declaration: $file (expected class $class_name)"
        fail=1
    fi
done < <(find "$TARGET_DIR" -type f -name '*.cs' | sort)

echo
echo "Checking for duplicate class declarations..."
rg -No "class\\s+[A-Za-z_][A-Za-z0-9_]*" "$TARGET_DIR" -g '*.cs' \
    | sed -E 's/.*class\\s+([A-Za-z_][A-Za-z0-9_]*).*/\1/' \
    | LC_ALL=C sort > "$TMP_DIR/all_classes.txt"

if uniq -d "$TMP_DIR/all_classes.txt" | tee "$TMP_DIR/duplicate_classes.txt" | rg -q "."; then
    echo "  Duplicate class names found:"
    sed 's/^/    /' "$TMP_DIR/duplicate_classes.txt"
    fail=1
else
    echo "  No duplicate class names found."
fi

echo
echo "Checking known CardEffectClassName references..."
rg -No 'CardEffectClassName\\s*=\\s*"[^"]+"' "$ROOT_DIR/Assets/Scripts" \
    | sed -E 's/.*"([^"]+)".*/\1/' \
    | LC_ALL=C sort -u > "$TMP_DIR/referenced_effects.txt"

while IFS= read -r class_name; do
    if [[ -z "$class_name" ]]; then
        continue
    fi

    if ! rg -q "class\\s+${class_name}\\b" "$TARGET_DIR" -g '*.cs'; then
        echo "  Referenced effect class missing: $class_name"
        fail=1
    fi
done < "$TMP_DIR/referenced_effects.txt"

if [[ "$fail" -ne 0 ]]; then
    echo
    echo "Card script consistency check failed." >&2
    exit 1
fi

echo
echo "Card script consistency check passed."
