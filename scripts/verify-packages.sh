#!/usr/bin/env bash
#
# Release gate for the multi-target packages. Two modes, so CI can run them in different jobs:
#
#   pack <feed-dir>
#       Packs the solution into <feed-dir> and inspects every package:
#         - runtime libraries carry lib/<tfm>/ for exactly the supported frameworks, and a dependency group
#           for each;
#         - the net10.0 dependency group does not pull an 11.x Microsoft package, so .NET 10 consumers get
#           nothing new from adding net11.0;
#         - tools carry tools/<tfm>/any/ for each framework;
#         - the core package still carries its source generator (analyzers/) and buildTransitive/ assets.
#
#   consume <feed-dir> <tfm>
#       Builds and runs a small app outside the repository, targeting <tfm> and referencing the packages from
#       <feed-dir> only (nuget.org for everything else), and fetches its AsyncAPI document. CI runs this for
#       net10.0 in a job that installs only the .NET 10 SDK, which proves that .NET 10 consumers do not need
#       the newer SDK this repository builds with, and for net11.0 with only the .NET 11 SDK.
#
# Usage: scripts/verify-packages.sh pack <feed-dir>
#        scripts/verify-packages.sh consume <feed-dir> <tfm>
set -euo pipefail

FRAMEWORKS="net10.0 net11.0"
VERSION="99.0.0-ci"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

pack() {
  local feed
  mkdir -p "$1"
  feed="$(cd "$1" && pwd)"
  cd "$repo_root"
  echo "==> Packing the solution as $VERSION"
  dotnet pack src/Bielu.AspNetCore.AsyncApi.slnx -c Release -nologo -v q -o "$feed" -p:VersionPrefix=99.0.0 -p:VersionSuffix=ci

  echo "==> Inspecting the packages"
  python3 - "$feed" "$VERSION" $FRAMEWORKS <<'PY'
import re, sys, zipfile, pathlib
import xml.etree.ElementTree as ET

feed, version, frameworks = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3:]
expected = set(frameworks)
errors, checked = [], 0
# Microsoft packages released with the shared framework: their major must match the group's framework.
framework_aligned = re.compile(r'^(Microsoft\.AspNetCore\.|Microsoft\.Extensions\.|System\.)', re.I)

for package in sorted(feed.glob(f'*.{version}.nupkg')):
    name = package.name[:-len(f'.{version}.nupkg')]
    with zipfile.ZipFile(package) as archive:
        entries = archive.namelist()
        nuspec = ET.fromstring(archive.read(next(e for e in entries if e.endswith('.nuspec'))))
    lib = {m.group(1) for e in entries if (m := re.match(r'lib/([^/]+)/', e))}
    tools = {m.group(1) for e in entries if (m := re.match(r'tools/([^/]+)/any/', e))}
    ns = {'n': nuspec.tag.split('}')[0].strip('{')}
    groups = {g.get('targetFramework', '').lower(): g for g in nuspec.iterfind('.//n:dependencies/n:group', ns)}

    if lib:
        checked += 1
        if lib != expected:
            errors.append(f'{name}: lib/ has {sorted(lib)}, expected {sorted(expected)}')
        for tfm in expected:
            if tfm not in groups:
                errors.append(f'{name}: no dependency group for {tfm} (has {sorted(groups)})')
        for dependency in groups.get('net10.0', ET.Element('none')).iterfind('n:dependency', ns):
            dep_id, dep_version = dependency.get('id', ''), dependency.get('version', '')
            if framework_aligned.match(dep_id) and re.match(r'[\[(]?11\.', dep_version):
                errors.append(f'{name}: net10.0 depends on {dep_id} {dep_version}')
    if tools:
        checked += 1
        if tools != expected:
            errors.append(f'{name}: tools/ has {sorted(tools)}, expected {sorted(expected)}')
    if name == 'Bielu.AspNetCore.AsyncApi':
        if not any(e.startswith('analyzers/dotnet/cs/') and 'SourceGenerators' in e for e in entries):
            errors.append(f'{name}: the source generator is missing from analyzers/dotnet/cs/')
        if not any(e.startswith('buildTransitive/') for e in entries):
            errors.append(f'{name}: buildTransitive/ assets are missing')

if checked == 0:
    errors.append(f'no packages with lib/ or tools/ assets found in {feed}')
for error in errors:
    print(f'::error::{error}', file=sys.stderr)
print(f'    {checked} packages inspected, {len(errors)} problem(s)')
sys.exit(1 if errors else 0)
PY
}

consume() {
  local feed tfm work
  # Native path for NuGet.config: Git Bash on Windows reports /c/... or /tmp/..., which NuGet cannot open.
  # `pwd -W` gives C:/... there and does not exist elsewhere.
  feed="$(cd "$1" && (pwd -W 2>/dev/null || pwd))"
  tfm="$2"
  work="$(mktemp -d)"
  # shellcheck disable=SC2064 # expand now: $work is local
  trap "rm -rf '$work'" EXIT

  # CONSUMER_SDK pins the consumer's SDK (e.g. 10.0.100) through its own global.json, to check locally
  # what CI checks with a job that installs nothing newer.
  if [ -n "${CONSUMER_SDK:-}" ]; then
    printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "$CONSUMER_SDK" > "$work/global.json"
  fi

  echo "==> Consuming the packages from $feed as a $tfm app (SDK $(cd "$work" && dotnet --version))"
  cat > "$work/NuGet.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="local" value="$feed" />
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
  cat > "$work/Consumer.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Bielu.AspNetCore.AsyncApi" Version="$VERSION" />
    <PackageReference Include="Bielu.AspNetCore.AsyncApi.Attributes" Version="$VERSION" />
  </ItemGroup>
</Project>
XML
  cat > "$work/Program.cs" <<'CS'
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAsyncApi(options => options.WithInfo("Consumer", "1.0.0"));
var app = builder.Build();
app.MapAsyncApi();
app.Run();

public record OrderPlaced(Guid OrderId, decimal Total);

[AsyncApi]
public class OrderBus
{
    [Channel("orders")]
    [SendOperation(typeof(OrderPlaced))]
    public void Publish(OrderPlaced order)
    {
    }
}
CS

  # From inside the consumer's directory: the SDK is chosen by the global.json above the *current*
  # directory, and the repository's own one pins the newer SDK this job is meant to do without.
  cd "$work"
  dotnet build Consumer.csproj -c Release -nologo -v q -o "$work/out"

  local port=5198 document="$work/document.json"
  dotnet "$work/out/Consumer.dll" --urls "http://127.0.0.1:$port" >"$work/app.log" 2>&1 &
  local pid=$! ready=0
  for _ in $(seq 1 60); do
    if curl -fsS "http://127.0.0.1:$port/asyncapi/v1.json" -o "$document" 2>/dev/null; then
      ready=1
      break
    fi
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true

  if [ "$ready" -ne 1 ] || ! grep -qi 'orderplaced' "$document"; then
    echo "::error::The $tfm consumer did not serve its AsyncAPI document" >&2
    sed 's/^/    /' "$work/app.log" >&2
    exit 1
  fi
  echo "    $tfm consumer serves its document"
}

case "${1:-}" in
  pack) [ $# -eq 2 ] || { echo "usage: $0 pack <feed-dir>" >&2; exit 2; }; pack "$2" ;;
  consume) [ $# -eq 3 ] || { echo "usage: $0 consume <feed-dir> <tfm>" >&2; exit 2; }; consume "$2" "$3" ;;
  *) echo "usage: $0 pack <feed-dir> | consume <feed-dir> <tfm>" >&2; exit 2 ;;
esac
