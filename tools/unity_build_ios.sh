#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_VERSION_FILE="$ROOT_DIR/ProjectSettings/ProjectVersion.txt"
UNITY_VERSION="$(awk -F': ' '/m_EditorVersion:/{print $2}' "$PROJECT_VERSION_FILE")"
UNITY_BIN_DEFAULT="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_BIN="${UNITY_BIN:-$UNITY_BIN_DEFAULT}"

OUTPUT_PATH="${1:-$ROOT_DIR/Builds/iOS}"
IOS_SDK="${2:-${DCGO_IOS_SDK:-device}}"
OFFLINE_BOOT="${3:-${DCGO_OFFLINE_BOOT:-1}}"
LOG_DIR="$ROOT_DIR/Logs"
LOG_FILE="$LOG_DIR/ios-build.log"
mkdir -p "$LOG_DIR"

if [[ ! -x "$UNITY_BIN" ]]; then
    echo "Unity binary not found: $UNITY_BIN" >&2
    echo "Set UNITY_BIN to your Unity executable path." >&2
    exit 1
fi

export DCGO_IOS_BUILD_PATH="$OUTPUT_PATH"
export DCGO_IOS_SDK="$IOS_SDK"
export DCGO_OFFLINE_BOOT="$OFFLINE_BOOT"

echo "Building iOS project to: $OUTPUT_PATH (sdk=$IOS_SDK, offline_boot=$OFFLINE_BOOT)"
"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$ROOT_DIR" \
    -executeMethod BuildCI.BuildIOS \
    -logFile "$LOG_FILE"

if rg -n "error CS|Scripts have compiler errors|BuildFailedException|Exception: iOS build failed" "$LOG_FILE" >/dev/null; then
    echo "iOS build failed. See log: $LOG_FILE" >&2
    rg -n "error CS|Scripts have compiler errors|BuildFailedException|Exception: iOS build failed" "$LOG_FILE" || true
    exit 1
fi

echo "iOS build completed. Log: $LOG_FILE"
