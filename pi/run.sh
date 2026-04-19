#!/usr/bin/env bash
# Launcher for the Pi edge server on Linux / macOS.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE"

if [ ! -d ".venv" ]; then
    echo "[run.sh] Creating virtual env in .venv ..."
    python3 -m venv .venv
fi

# shellcheck disable=SC1091
source .venv/bin/activate

pip install --quiet --upgrade pip
pip install --quiet -r requirements.txt

exec python -m src.main "$@"
