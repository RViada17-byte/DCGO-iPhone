#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_VERSION_FILE="$ROOT_DIR/ProjectSettings/ProjectVersion.txt"
UNITY_VERSION="$(awk -F': ' '/m_EditorVersion:/{print $2}' "$PROJECT_VERSION_FILE")"
UNITY_BIN_DEFAULT="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_BIN="${UNITY_BIN:-$UNITY_BIN_DEFAULT}"

LOG_DIR="$ROOT_DIR/Logs"
LOG_FILE="$LOG_DIR/compile-check.log"
mkdir -p "$LOG_DIR"

if [[ ! -x "$UNITY_BIN" ]]; then
    echo "Unity binary not found: $UNITY_BIN" >&2
    echo "Set UNITY_BIN to your Unity executable path." >&2
    exit 1
fi

echo "Running Unity compile check with: $UNITY_BIN"
"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$ROOT_DIR" \
    -logFile "$LOG_FILE"

if rg -n "error CS|Scripts have compiler errors|Compilation failed|BuildFailedException" "$LOG_FILE" >/dev/null; then
    echo "Compile check failed. See log: $LOG_FILE" >&2
    rg -n "error CS|Scripts have compiler errors|Compilation failed|BuildFailedException" "$LOG_FILE" || true
    exit 1
fi

echo "Compile check passed. Log: $LOG_FILE"
