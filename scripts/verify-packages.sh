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
#   consume-wolverine <feed-dir> <tfm> <wolverinefx-version>...
#       Runs the Wolverine integration's test suite for <tfm> against the packed integration from <feed-dir>,
#       once per WolverineFx version, each from a fresh copy outside the repository with its own package
#       folder. Checks that each run resolved the packed integration and the requested WolverineFx, so the
#       one package built against the minimum WolverineFx is proven on every major it claims.
#
# Usage: scripts/verify-packages.sh pack <feed-dir>
#        scripts/verify-packages.sh consume <feed-dir> <tfm>
#        scripts/verify-packages.sh consume-wolverine <feed-dir> <tfm> <wolverinefx-version>...
set -euo pipefail

FRAMEWORKS="net10.0 net11.0"
VERSION="99.0.0-ci"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# The WolverineFx version the Wolverine integration builds against by default, so the minimum its package
# declares.
MIN_WOLVERINE="$(sed -n 's/.*<WolverineFxVersion[^>]*>\([^<]*\)<.*/\1/p' "$repo_root/Directory.Packages.props")"

pack() {
  local feed
  mkdir -p "$1"
  feed="$(cd "$1" && pwd)"
  cd "$repo_root"
  echo "==> Packing the solution as $VERSION"
  dotnet pack src/Bielu.AspNetCore.AsyncApi.slnx -c Release -nologo -v q -o "$feed" -p:VersionPrefix=99.0.0 -p:VersionSuffix=ci

  echo "==> Inspecting the packages"
  MIN_WOLVERINE="$MIN_WOLVERINE" python3 - "$feed" "$VERSION" $FRAMEWORKS <<'PY'
import os, re, sys, zipfile, pathlib
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
    if name == 'Bielu.AspNetCore.AsyncApi.Wolverine':
        # One package serves every WolverineFx major it supports, so each framework must ask for the
        # minimum only: a bare NuGet version means "this or newer".
        minimum = os.environ['MIN_WOLVERINE']
        for tfm in sorted(expected):
            wolverine = [d.get('version') for d in groups.get(tfm, ET.Element('none')).iterfind('n:dependency', ns)
                         if d.get('id') == 'WolverineFx']
            if wolverine != [minimum]:
                errors.append(f'{name}: {tfm} depends on WolverineFx {wolverine}, expected [{minimum!r}]')

if checked == 0:
    errors.append(f'no packages with lib/ or tools/ assets found in {feed}')
for error in errors:
    print(f'::error::{error}', file=sys.stderr)
print(f'    {checked} packages inspected, {len(errors)} problem(s)')
sys.exit(1 if errors else 0)
PY
}

# Native path for NuGet.config: Git Bash on Windows reports /c/... or /tmp/..., which NuGet cannot open.
# `pwd -W` gives C:/... there and does not exist elsewhere.
native_path() {
  (cd "$1" && (pwd -W 2>/dev/null || pwd))
}

# Makes <dir> a place to restore from <feed> outside the repository: Bielu.* from the feed only, the rest
# from nuget.org. CONSUMER_SDK pins its SDK (e.g. 10.0.100) through a global.json of its own, to check
# locally what CI checks with a job that installs nothing newer.
prepare_consumer_dir() {
  local dir="$1" feed="$2"
  if [ -n "${CONSUMER_SDK:-}" ]; then
    printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "$CONSUMER_SDK" > "$dir/global.json"
  fi
  cat > "$dir/NuGet.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="packed" value="$feed" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="packed">
      <package pattern="Bielu.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
XML
}

consume() {
  local feed tfm work
  feed="$(native_path "$1")"
  tfm="$2"
  work="$(mktemp -d)"
  # shellcheck disable=SC2064 # expand now: $work is local
  trap "rm -rf '$work'" EXIT

  prepare_consumer_dir "$work" "$feed"
  echo "==> Consuming the packages from $feed as a $tfm app (SDK $(cd "$work" && dotnet --version))"
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

consume_wolverine() {
  local feed tfm wolverine work project assets
  feed="$(native_path "$1")"
  tfm="$2"
  shift 2
  project="test/Bielu.AspNetCore.AsyncApi.Wolverine.Tests"

  for wolverine in "$@"; do
    work="$(mktemp -d)"
    # shellcheck disable=SC2064 # expand now: $work is local
    trap "rm -rf '$work'" EXIT

    # A copy of the test project with the props it imports, outside the repository so the SDK is the job's
    # own, and with a package folder of its own so nothing restored for another version is reused.
    cp "$repo_root/Directory.Build.props" "$repo_root/Directory.Packages.props" "$repo_root/coverlet.runsettings" "$work/"
    mkdir -p "$work/test"
    cp -r "$repo_root/$project" "$work/test/"
    rm -rf "$work/$project/bin" "$work/$project/obj" "$work/$project/TestResults"
    prepare_consumer_dir "$work" "$feed"

    cd "$work"
    echo "==> Testing the packed Wolverine integration on $tfm with WolverineFx $wolverine (SDK $(dotnet --version))"
    local properties=(
      "-p:TargetFrameworks=$tfm"
      "-p:WolverineFxVersion=$wolverine"
      "-p:BieluWolverinePackageVersion=$VERSION"
      "-p:RestorePackagesPath=$(native_path "$work")/packages"
    )
    dotnet restore "$project" -nologo -v q "${properties[@]}"

    assets="$work/$project/obj/project.assets.json"
    for expected in "Bielu.AspNetCore.AsyncApi.Wolverine/$VERSION" "WolverineFx/$wolverine"; do
      if ! grep -q "\"$expected\": {" "$assets"; then
        echo "::error::The $tfm Wolverine tests did not resolve $expected" >&2
        exit 1
      fi
    done

    dotnet test "$project" -c Release -nologo --no-restore "${properties[@]}"
    cd "$repo_root"
    rm -rf "$work"
  done
}

case "${1:-}" in
  pack) [ $# -eq 2 ] || { echo "usage: $0 pack <feed-dir>" >&2; exit 2; }; pack "$2" ;;
  consume) [ $# -eq 3 ] || { echo "usage: $0 consume <feed-dir> <tfm>" >&2; exit 2; }; consume "$2" "$3" ;;
  consume-wolverine)
    [ $# -ge 4 ] || { echo "usage: $0 consume-wolverine <feed-dir> <tfm> <wolverinefx-version>..." >&2; exit 2; }
    consume_wolverine "$2" "$3" "${@:4}" ;;
  *) echo "usage: $0 pack <feed-dir> | consume <feed-dir> <tfm> | consume-wolverine <feed-dir> <tfm> <wolverinefx-version>..." >&2; exit 2 ;;
esac
