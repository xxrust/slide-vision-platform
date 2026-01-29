#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
TOOL="codex"
ITER=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tool)
      TOOL="$2"
      shift 2
      ;;
    *)
      ITER="$1"
      shift
      ;;
  esac
done

if [[ "$TOOL" != "codex" ]]; then
  echo "Only codex tool is supported in this repo."
  exit 1
fi

for i in $(seq 1 "$ITER"); do
  codex exec --dangerously-bypass-approvals-and-sandbox -C "$SCRIPT_DIR" - < "$SCRIPT_DIR/CODEX.md"
done
