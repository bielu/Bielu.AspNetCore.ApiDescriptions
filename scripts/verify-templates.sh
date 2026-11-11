#!/usr/bin/env bash
#
# Verifies the `dotnet new` template pack end to end: packs it, installs it into a throwaway template hive (so the
# machine's own templates are untouched), then generates every template for every supported framework outside the
# repository, so none of its Directory.Build/Packages props leak in, and builds the result. For each one it checks
# that the Framework choice actually reached the generated projects: TargetFramework, and the
# Microsoft.Extensions.Hosting version that has to match it.
#
# The generated projects reference the Bielu.AspNetCore.AsyncApi packages from nuget.org at the version the
# templates pin, so this needs network access.
#
# Usage: scripts/verify-templates.sh
set -euo pipefail

WORK="$(mktemp -d)"
[ "${KEEP_WORK:-}" = "1" ] || trap 'rm -rf "$WORK"' EXIT

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

TEMPLATES=(asyncapi-webapi asyncapi-signalr asyncapi-grpc asyncapi-console asyncapi-sln)
FRAMEWORKS=(net10.0 net11.0)
declare -A HOSTING_VERSION=([net10.0]=10.0.15 [net11.0]=11.0.0)
HIVE="$WORK/hive"

echo "==> Packing the template pack"
dotnet pack src/Bielu.AspNetCore.AsyncApi.Templates -c Release -nologo -v q -o "$WORK/feed" -p:VersionPrefix=99.0.0 -p:VersionSuffix=local
dotnet new install "$WORK/feed/Bielu.AspNetCore.AsyncApi.Templates.99.0.0-local.nupkg" --debug:custom-hive "$HIVE" >/dev/null

for template in "${TEMPLATES[@]}"; do
  for tfm in "${FRAMEWORKS[@]}"; do
    out="$WORK/generated/$template-$tfm"
    echo "==> dotnet new $template --Framework $tfm"
    dotnet new "$template" -n Sample -o "$out" --Framework "$tfm" --debug:custom-hive "$HIVE" >/dev/null

    while IFS= read -r project; do
      if ! grep -q "<TargetFramework>$tfm</TargetFramework>" "$project"; then
        echo "::error::$project does not target $tfm:" >&2
        grep -n "TargetFramework" "$project" >&2 || true
        exit 1
      fi
      if grep -q 'Microsoft.Extensions.Hosting' "$project" \
        && ! grep -q "Microsoft.Extensions.Hosting\" Version=\"${HOSTING_VERSION[$tfm]}\"" "$project"; then
        echo "::error::$project does not reference Microsoft.Extensions.Hosting ${HOSTING_VERSION[$tfm]} for $tfm:" >&2
        grep -n "Microsoft.Extensions.Hosting" "$project" >&2
        exit 1
      fi
    done < <(find "$out" -name '*.csproj')

    target="$out"
    solution="$(find "$out" -maxdepth 1 -name '*.sln' -o -maxdepth 1 -name '*.slnx' | head -n 1)"
    [ -n "$solution" ] && target="$solution"
    dotnet build "$target" -c Release -nologo -v q
    echo "    OK"
  done
done

echo "Every template generates and builds for ${FRAMEWORKS[*]}."
