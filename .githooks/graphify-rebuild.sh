#!/usr/bin/env bash
# .githooks/graphify-rebuild.sh
# Refresh the Graphify code knowledge graph after a git operation.
#
# Runs `graphify update .` — the FAST, no-LLM path — in a detached background
# process so git never blocks. Invoked by the post-commit / post-merge /
# post-checkout / post-rewrite hooks in this directory.
#
# Guarantees:
#   - Never blocks the git operation (child is detached; all fds off git).
#   - Never fails a git operation (always exits 0).
#   - Single-flight: a PID lock prevents rapid git ops from stacking rebuilds.
#   - Quiet no-op if graphify isn't installed.
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
cd "$ROOT" 2>/dev/null || exit 0

# Resolve graphify even under git's minimal hook PATH (uv installs to ~/.local/bin).
BIN="$(command -v graphify 2>/dev/null || true)"
if [ -z "$BIN" ]; then
  for d in "$HOME/.local/bin" "/usr/local/bin" "/opt/homebrew/bin"; do
    for n in graphify graphify.exe; do
      [ -x "$d/$n" ] && { BIN="$d/$n"; break 2; }
    done
  done
fi
[ -n "$BIN" ] || exit 0   # graphify not installed -> skip silently

OUT="graphify-out"
mkdir -p "$OUT" 2>/dev/null || true
LOCK="$OUT/.rebuild.lock"

# Skip if a rebuild is already in flight (stale-tolerant via kill -0).
if [ -f "$LOCK" ]; then
  PID="$(cat "$LOCK" 2>/dev/null || echo "")"
  [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null && exit 0
fi

# Detach: the child holds the lock and removes it on exit; git returns at once.
(
  trap 'rm -f "$LOCK"' EXIT
  "$BIN" update . >"$OUT/.rebuild.log" 2>&1
) </dev/null >/dev/null 2>&1 &
CHILD=$!
echo "$CHILD" >"$LOCK" 2>/dev/null || true
disown "$CHILD" 2>/dev/null || true
exit 0
