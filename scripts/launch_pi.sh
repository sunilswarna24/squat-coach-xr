#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# Launch the Pi edge server detached, with its log tee'd to pi/server.log.
#
# Designed to be idempotent: kills any previous `src.main` process before
# starting a new one, so you can re-run this script repeatedly without
# leaving orphaned servers behind.
#
# Usage (on the Pi):
#   ./scripts/launch_pi.sh                # default host / port / fps
#   HOST=0.0.0.0 PORT=8765 FPS=30 ./scripts/launch_pi.sh
#
# The server logs to pi/server.log and binds on $PORT (8765 by default).
# -----------------------------------------------------------------------------
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/.." && pwd)"

HOST="${HOST:-0.0.0.0}"
PORT="${PORT:-8765}"
CAMERA="${CAMERA:-0}"
FPS="${FPS:-30}"

VENV="${VENV:-$REPO/.venv}"
if [ ! -d "$VENV" ]; then
    echo "No venv at $VENV. Create it with:" >&2
    echo "  python3 -m venv $VENV && source $VENV/bin/activate && pip install -r pi/requirements.txt" >&2
    exit 1
fi
# shellcheck disable=SC1091
source "$VENV/bin/activate"

pkill -f "src.main" 2>/dev/null || true
sleep 0.4

LOG="$REPO/pi/server.log"
: > "$LOG"

cd "$REPO/pi"
nohup python -m src.main --host "$HOST" --port "$PORT" --camera "$CAMERA" --fps "$FPS" \
    > "$LOG" 2>&1 &
PID=$!
echo "PID=$PID"
sleep 3
echo "--- first log lines ---"
head -40 "$LOG"
echo "--- listening sockets ---"
ss -ltn | grep ":$PORT " || echo "  port $PORT NOT listening yet"
echo "--- process ---"
ps -p "$PID" -o pid,rss,stat,cmd 2>/dev/null || echo "  process exited"
