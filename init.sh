#!/usr/bin/env bash
# Windows + Git Bash. PowerShell owns the shared restore/build/test pipeline.
set -Eeuo pipefail
SOURCE_DIR=${BASH_SOURCE[0]}
if [[ "$SOURCE_DIR" == */* ]]; then
    SOURCE_DIR=${SOURCE_DIR%/*}
else
    SOURCE_DIR=.
fi
ROOT="$(cd -- "$SOURCE_DIR" && pwd -P)"
if [[ -n "${POWERSHELL_EXE:-}" ]]; then
    PS="$POWERSHELL_EXE"
elif command -v pwsh.exe >/dev/null 2>&1; then
    PS=pwsh.exe
else
    printf 'PowerShell 7.4+ (pwsh.exe) is required; set POWERSHELL_EXE if not on PATH.\n' >&2
    exit 2
fi
exec "$PS" -NoProfile -ExecutionPolicy Bypass -File "$ROOT/init.ps1" "$@"
