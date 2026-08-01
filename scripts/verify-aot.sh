#!/usr/bin/env bash
#
# Verifies the Native AOT story end to end, which is what roadmap PR 9 scoped but never wired up.
#
# Two things can break independently, so both are checked:
#   1. the AOT publish itself succeeds, with no trim/AOT analysis warnings; and
#   2. the AOT binary serves the *same* AsyncAPI document as the ordinary reflection-based build.
#
# (2) is the one that matters. The source generator replaces the reflection scan under AOT, so a
# silent divergence between the two providers would produce a valid-looking but wrong document —
# exactly the failure a smoke test that only checks "it starts" would miss.
#
# Usage: scripts/verify-aot.sh [runtime-identifier]
set -euo pipefail

RID="${1:-linux-x64}"
PROJECT="src/examples/AotStreetlights"
DOCUMENT_PATH="/asyncapi/Streetlights.json"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# Serve the document from a running instance and print it. Takes the executable and a port.
fetch_document() {
  local exe="$1" port="$2" out="$3"
  "$exe" --urls "http://127.0.0.1:${port}" >"${out}.log" 2>&1 &
  local pid=$!

  local ready=0
  for _ in $(seq 1 60); do
    if curl -fsS "http://127.0.0.1:${port}${DOCUMENT_PATH}" -o "$out" 2>/dev/null; then
      ready=1
      break
    fi
    # If the process died, there is nothing to wait for.
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done

  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true

  if [ "$ready" -ne 1 ]; then
    echo "::error::$exe never served ${DOCUMENT_PATH}" >&2
    sed 's/^/    /' "${out}.log" >&2
    return 1
  fi
}

echo "==> Publishing $PROJECT as Native AOT ($RID)"
# PublishAot is deliberately NOT passed with -p: here. It is already set in AotStreetlights.csproj,
# and a -p: value becomes a *global* property that flows into every referenced project — including
# the netstandard2.0 analyzer and source generator, which cannot be AOT-compiled and fail the whole
# publish with NETSDK1207. A global property also cannot be overridden by those projects.
#
# Not promoting IL2xxx/IL3xxx to errors yet: the reflection-based provider is still the default and
# is reachable from the app graph, so ILC legitimately reports warnings for it (as does ByteBard's
# own assembly, IL2104/IL3053, which is outside our control). Turn that on once the remaining trim
# annotations on AsyncApiDocumentService / MapAsyncApi / AddAsyncApi are settled — this is the right
# place to assert it, since it is where ILC actually runs.
dotnet publish "$PROJECT" -c Release -r "$RID" -o "$WORK/aot"

echo "==> Publishing the same project without AOT, for comparison"
dotnet publish "$PROJECT" -c Release -p:PublishAot=false -o "$WORK/jit"

# Both runs must use the SAME port. The generated document records the bound address as its server
# host, so serving the two builds on different ports would make them differ on that field alone and
# fail the comparison for a reason that has nothing to do with AOT.
PORT=5199

echo "==> Fetching the document from the AOT build"
fetch_document "$WORK/aot/AotStreetlights" "$PORT" "$WORK/aot.json"

echo "==> Fetching the document from the reflection-based build"
fetch_document "$WORK/jit/AotStreetlights" "$PORT" "$WORK/jit.json"

echo "==> Comparing"
# Compared as parsed JSON, not as bytes: property ordering is not part of the contract.
if ! python3 -c "
import json, sys
with open(sys.argv[1]) as f: aot = json.load(f)
with open(sys.argv[2]) as f: jit = json.load(f)
if aot == jit:
    print('    documents are identical')
    sys.exit(0)
print('    AOT and reflection-based documents differ', file=sys.stderr)
print('--- AOT ---', file=sys.stderr)
print(json.dumps(aot, indent=2, sort_keys=True), file=sys.stderr)
print('--- reflection ---', file=sys.stderr)
print(json.dumps(jit, indent=2, sort_keys=True), file=sys.stderr)
sys.exit(1)
" "$WORK/aot.json" "$WORK/jit.json"; then
  echo "::error::The Native AOT build does not produce the same AsyncAPI document as the reflection-based build." >&2
  exit 1
fi

echo "==> Native AOT verification passed"
