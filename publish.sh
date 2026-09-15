#!/usr/bin/env bash
# publish.sh — build and publish @vc-surveil/api + @vc-surveil/surveil
# Usage:
#   ./publish.sh              # build + publish both (public)
#   ./publish.sh --dry-run    # build + npm publish --dry-run
#   ./publish.sh --api-only   # publish only api
#   ./publish.sh --cli-only   # publish only cli
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

DRY_RUN=""
API_ONLY=""
CLI_ONLY=""
TAG="latest"
ACCESS="public"

for arg in "$@"; do
  case "$arg" in
    --dry-run|--dry) DRY_RUN="--dry-run" ;;
    --api-only) API_ONLY=1 ;;
    --cli-only) CLI_ONLY=1 ;;
    --tag=*) TAG="${arg#--tag=}" ;;
    --access=*) ACCESS="${arg#--access=}" ;;
    -h|--help)
      echo "Usage: $0 [--dry-run] [--api-only|--cli-only] [--tag=TAG] [--access=public|restricted]"
      exit 0
      ;;
    *) echo "unknown arg: $arg" >&2; exit 1 ;;
  esac
done

need() { command -v "$1" >/dev/null 2>&1 || { echo "missing required command: $1" >&2; exit 1; }; }
need node
need npm
need npx

if [ -z "${CLI_ONLY:-}" ]; then
  echo "==> checking npm auth (npm whoami)..."
  if ! npm whoami >/dev/null 2>&1; then
    echo "warn: npm whoami failed — are you logged in? Run: npm login" >&2
    if [ -z "$DRY_RUN" ]; then
      echo "abort: not logged in and not --dry-run" >&2
      exit 1
    fi
  else
    echo "   logged in as $(npm whoami)"
  fi
fi

echo "==> building workspaces..."
if [ -f "$ROOT/node_modules/.bin/tsc" ]; then
  npm run build
else
  # devenv / CI path — ensure deps then build
  if [ ! -d "$ROOT/node_modules" ]; then
    echo "   npm install --ignore-scripts..."
    npm install --ignore-scripts
  fi
  npm run build
fi

echo "==> verifying builds..."
test -f "$ROOT/packages/api/dist/index.d.ts" || { echo "missing packages/api/dist/index.d.ts — build failed" >&2; exit 1; }
test -f "$ROOT/packages/api/dist/index.js"  || { echo "missing packages/api/dist/index.js — build failed" >&2; exit 1; }
test -f "$ROOT/packages/cli/dist/cli.js"    || { echo "missing packages/cli/dist/cli.js — build failed" >&2; exit 1; }
echo "   api: $(ls -lh packages/api/dist/index.d.ts | awk '{print $9, $5}')"
echo "   cli: $(ls -lh packages/cli/dist/cli.js | awk '{print $9, $5}')"

# npm pack preview (what would be published)
echo "==> pack preview..."
npm pack --dry-run -w @vc-surveil/api 2>&1 | tail -n 30 || true
npm pack --dry-run -w @vc-surveil/surveil 2>&1 | tail -n 30 || true

publish_one() {
  local ws="$1"
  local name
  name="$(node -p "require('./packages/${ws#@vc-surveil/}/package.json').name" 2>/dev/null || echo "$ws")"
  # Fallback for api
  if [ "$ws" = "@vc-surveil/api" ]; then
    name="@vc-surveil/api"
  fi
  echo "==> publishing ${name} (tag=${TAG}, access=${ACCESS}) ${DRY_RUN:+(dry-run)}..."
  # Use npm publish with provenance if available, fallback gracefully
  if [ -n "$DRY_RUN" ]; then
    npm publish --dry-run --access "$ACCESS" --tag "$TAG" -w "$ws"
  else
    npm publish --access "$ACCESS" --tag "$TAG" -w "$ws"
  fi
  echo "   published ${name}"
}

if [ -n "${CLI_ONLY:-}" ]; then
  publish_one "@vc-surveil/surveil"
elif [ -n "${API_ONLY:-}" ]; then
  publish_one "@vc-surveil/api"
else
  # Publish api first (cli depends on api types, not runtime)
  publish_one "@vc-surveil/api"
  publish_one "@vc-surveil/surveil"
fi

echo "done."
echo "  api: npm view @vc-surveil/api version"
echo "  cli: npm view @vc-surveil/surveil version   # install via: npx @vc-surveil/surveil --help"
