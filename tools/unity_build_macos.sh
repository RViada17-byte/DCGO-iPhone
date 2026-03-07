#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_VERSION_FILE="$ROOT_DIR/ProjectSettings/ProjectVersion.txt"
UNITY_VERSION="$(awk -F': ' '/m_EditorVersion:/{print $2}' "$PROJECT_VERSION_FILE")"
UNITY_BIN_DEFAULT="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_BIN="${UNITY_BIN:-$UNITY_BIN_DEFAULT}"

OUTPUT_PATH="${1:-$ROOT_DIR/Builds/Mac/DCGO.app}"
LOG_DIR="$ROOT_DIR/Logs"
LOG_FILE="$LOG_DIR/macos-build.log"
mkdir -p "$LOG_DIR"

if [[ ! -x "$UNITY_BIN" ]]; then
    echo "Unity binary not found: $UNITY_BIN" >&2
    echo "Set UNITY_BIN to your Unity executable path." >&2
    exit 1
fi

mkdir -p "$(dirname "$OUTPUT_PATH")"
rm -rf "$OUTPUT_PATH"

echo "Building macOS app to: $OUTPUT_PATH"
"$UNITY_BIN" \
    -batchmode \
    -quit \
    -projectPath "$ROOT_DIR" \
    -buildOSXUniversalPlayer "$OUTPUT_PATH" \
    -logFile "$LOG_FILE"

if rg -n "error CS|Scripts have compiler errors|BuildFailedException|Build Finished, Result: Failed|Exception:" "$LOG_FILE" >/dev/null; then
    echo "macOS build failed. See log: $LOG_FILE" >&2
    rg -n "error CS|Scripts have compiler errors|BuildFailedException|Build Finished, Result: Failed|Exception:" "$LOG_FILE" || true
    exit 1
fi

echo "Cleaning app bundle metadata"
xattr -cr "$OUTPUT_PATH" || true

echo "Applying ad-hoc signature"
codesign --force --deep --sign - --timestamp=none "$OUTPUT_PATH"

echo "Verifying bundle signature"
codesign --verify --deep --strict --verbose=2 "$OUTPUT_PATH"

echo "macOS build completed. Log: $LOG_FILE"
echo "App: $OUTPUT_PATH"
