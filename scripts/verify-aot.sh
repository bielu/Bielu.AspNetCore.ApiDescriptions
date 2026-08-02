#!/usr/bin/env bash
#
# Verifies the Native AOT story end to end, which is what roadmap PR 9 scoped but never wired up.
#
# Three things can break independently, so all three are checked:
#   1. the AOT publish itself succeeds;
#   2. the AOT binary serves the *same* AsyncAPI document as the ordinary reflection-based build; and
#   3. an app consuming the *package* (not a project reference) gets the same result.
#
# (2) is the one that matters most. The source generator replaces the reflection scan under AOT, so a
# silent divergence between the two providers would produce a valid-looking but wrong document —
# exactly the failure a smoke test that only checks "it starts" would miss.
#
# (3) exists because AOT generation depends on runtime directives that reach consumers only through
# the package's buildTransitive/ folder. A ProjectReference does not flow those, so the project-
# reference build imports them by hand and cannot notice a wrong PackagePath — the failure would land
# on consumers and nowhere else. This publishes the example against a locally packed feed, which is
# the only way to exercise NuGet's own import of them.
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

# ---------------------------------------------------------------------------------------------
# The packaged-consumer publish. Everything above uses project references; this is the only part
# that exercises what NuGet actually hands a consumer.
# ---------------------------------------------------------------------------------------------
echo "==> Packing the library so the example can consume it as a package"
FEED="$WORK/feed"
# Attributes as well as the core package: core depends on it, and restoring core from a local feed
# means its dependency has to resolve from somewhere other than nuget.org, where this version does
# not exist yet.
dotnet pack src/Bielu.AspNetCore.AsyncApi/Bielu.AspNetCore.AsyncApi.csproj -c Release -o "$FEED" --nologo -v quiet
dotnet pack src/Bielu.AspNetCore.AsyncApi.Attributes/Bielu.AspNetCore.AsyncApi.Attributes.csproj -c Release -o "$FEED" --nologo -v quiet

PACKAGE_VERSION="$(dotnet msbuild src/Bielu.AspNetCore.AsyncApi/Bielu.AspNetCore.AsyncApi.csproj \
  -getProperty:PackageVersion -nologo | tr -d '[:space:]')"
echo "    packed version $PACKAGE_VERSION"

# The repository NuGet.config maps every package to nuget.org, so the local feed needs its own config
# rather than an -s flag: with package source mapping in force, an unmapped source is never consulted.
# It lives beside the copied project so restore picks it up by directory walk, and the repository's
# own configuration is left untouched.
CONSUMER="$WORK/consumer"
mkdir -p "$CONSUMER"
cp src/examples/AotStreetlights/Program.cs src/examples/AotStreetlights/AotStreetlights.csproj "$CONSUMER/"

# The consumer sits outside the repository, so it inherits none of its MSBuild conventions — which is
# the point, it should look like somebody else's app. It does need central package management, because
# the example's other PackageReference (Scalar.AspNetCore) carries no version of its own. Copy the
# repository's version list and append the version just packed.
python3 - "$repo_root/Directory.Packages.props" "$CONSUMER/Directory.Packages.props" "$PACKAGE_VERSION" <<'PY'
import sys
src, dst, version = sys.argv[1:4]
content = open(src, encoding='utf-8-sig').read()
entry = f'  <ItemGroup>\n    <PackageVersion Include="Bielu.AspNetCore.AsyncApi" Version="{version}" />\n  </ItemGroup>\n</Project>'
open(dst, 'w', encoding='utf-8').write(content.replace('</Project>', entry))
PY
cat > "$CONSUMER/NuGet.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="local" value="$FEED" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="Bielu.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
XML

echo "==> Publishing the example as a package consumer (Native AOT)"
# UseAsyncApiPackage swaps the project references for a PackageReference; the runtime directives now
# arrive through the package's buildTransitive/ folder, imported by NuGet rather than by hand.
dotnet publish "$CONSUMER/AotStreetlights.csproj" -c Release -r "$RID" -o "$WORK/pkg" \
  -p:UseAsyncApiPackage=true

echo "==> Fetching the document from the packaged-consumer build"
fetch_document "$WORK/pkg/AotStreetlights" "$PORT" "$WORK/pkg.json"

echo "==> Comparing"
# Compared as parsed JSON, not as bytes: property ordering is not part of the contract.
if ! python3 -c "
import json, sys

def load(path):
    with open(path) as f:
        return json.load(f)

labels = ['AOT (project reference)', 'reflection', 'AOT (package consumer)']
docs = [load(p) for p in sys.argv[1:]]
if all(d == docs[0] for d in docs):
    print('    all three documents are identical')
    sys.exit(0)
print('    documents differ', file=sys.stderr)
for label, doc in zip(labels, docs):
    print(f'--- {label} ---', file=sys.stderr)
    print(json.dumps(doc, indent=2, sort_keys=True), file=sys.stderr)
sys.exit(1)
" "$WORK/aot.json" "$WORK/jit.json" "$WORK/pkg.json"; then
  echo "::error::The Native AOT builds do not produce the same AsyncAPI document as the reflection-based build." >&2
  exit 1
fi

echo "==> Native AOT verification passed"
