#!/usr/bin/env bash
#
# Verifies that `dotnet asyncapi getdocument` generates the same document whichever runtime the CLI and the
# application run on. The CLI boots the application in-process, which only works on the runtime the application
# was built for; for any other one it relaunches itself on the application's runtime (see
# src/Bielu.AspNetCore.AsyncApi.Cli/Hosting/TargetRuntimeLauncher.cs). Unit tests cannot start a second runtime,
# so this runs the real thing:
#   1. both CLI builds (net10.0, net11.0) against StreetlightsAPI built for net10.0 and for net11.0. Every
#      combination must succeed, and all four documents must be identical (same app, same document,
#      whichever runtime generated it);
#   2. the packed tool, installed from a local feed as a consumer would install it, driven by the
#      ApiDescription.Server MSBuild target in a project that multi-targets net10.0;net11.0 with a custom
#      AsyncApiDocumentsDirectory. Each framework must get its own document, in its own subfolder.
#
# Needs the .NET 10 and .NET 11 runtimes (the SDK pinned in global.json plus the .NET 10 runtime).
#
# Usage: scripts/verify-cli-runtimes.sh
set -euo pipefail

WORK="$(mktemp -d)"
# KEEP_WORK=1 leaves the work directory behind for inspection.
[ "${KEEP_WORK:-}" = "1" ] || trap 'rm -rf "$WORK"' EXIT

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

FRAMEWORKS=(net10.0 net11.0)
CLI_PROJECT="src/Bielu.AspNetCore.AsyncApi.Cli"
CLI_DLL="Bielu.AspNetCore.AsyncApi.Cli.dll"
APP_PROJECT="src/examples/StreetlightsAPI"
APP_NAME="StreetlightsAPI"

echo "==> Building the CLI for ${FRAMEWORKS[*]}"
dotnet build "$CLI_PROJECT" -c Release -nologo -v q

for app_tfm in "${FRAMEWORKS[@]}"; do
  echo "==> Building $APP_NAME for $app_tfm"
  dotnet build "$APP_PROJECT" -c Release -nologo -v q -p:BieluExampleTargetFramework="$app_tfm" -o "$WORK/app-$app_tfm"
done

reference=""
for cli_tfm in "${FRAMEWORKS[@]}"; do
  for app_tfm in "${FRAMEWORKS[@]}"; do
    out="$WORK/out-cli-$cli_tfm-app-$app_tfm"
    echo "==> CLI on $cli_tfm, app built for $app_tfm"
    dotnet "$CLI_PROJECT/bin/Release/$cli_tfm/$CLI_DLL" getdocument \
      --assembly "$APP_NAME" \
      --assembly-path "$WORK/app-$app_tfm/$APP_NAME.dll" \
      --output "$out"

    document="$out/$APP_NAME.json"
    if [ ! -s "$document" ] || ! grep -q '"asyncapi"' "$document"; then
      echo "::error::CLI on $cli_tfm did not write an AsyncAPI document for the $app_tfm app. Found:" >&2
      ls -1 "$out" >&2 || true
      exit 1
    fi

    if [ -z "$reference" ]; then
      reference="$document"
    elif ! cmp -s "$reference" "$document"; then
      echo "::error::CLI on $cli_tfm with the $app_tfm app produced a different document than $reference:" >&2
      diff "$reference" "$document" >&2 || true
      exit 1
    fi
    echo "    OK"
  done
done

echo "==> Packing the CLI tool"
FEED="$WORK/feed"
TOOL_VERSION="99.0.0-local"
dotnet pack "$CLI_PROJECT" -c Release -nologo -v q -o "$FEED" -p:VersionPrefix=99.0.0 -p:VersionSuffix=local
package="$FEED/Bielu.AspNetCore.AsyncApi.Cli.$TOOL_VERSION.nupkg"
# Listed once into a variable: `unzip -l | grep -q` would trip pipefail when grep exits early.
package_listing="$(unzip -l "$package")"
for tfm in "${FRAMEWORKS[@]}"; do
  if ! grep -q "tools/$tfm/any/$CLI_DLL" <<<"$package_listing"; then
    echo "::error::The tool package has no tools/$tfm/any/$CLI_DLL" >&2
    grep "tools/" <<<"$package_listing" >&2 || true
    exit 1
  fi
done
echo "    tool package carries ${FRAMEWORKS[*]}"

echo "==> Multi-targeting consumer with the ApiDescription.Server targets"
CONSUMER="$WORK/consumer"
mkdir -p "$CONSUMER"
# Paths written into the project file must be native ones: Git Bash on Windows reports /c/..., which MSBuild
# cannot open. `pwd -W` gives C:/... there and does not exist elsewhere.
native_root="$(cd "$repo_root" && (pwd -W 2>/dev/null || pwd))"
targets_dir="$native_root/src/Bielu.AspNetCore.AsyncApi.ApiDescription.Server/build"
cat > "$CONSUMER/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <!-- Imported where NuGet would import the package's build/ assets. -->
  <Import Project="$targets_dir/Bielu.AspNetCore.AsyncApi.ApiDescription.Server.props" />
  <PropertyGroup>
    <TargetFrameworks>net10.0;net11.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AsyncApiDocumentsDirectory>docs</AsyncApiDocumentsDirectory>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$native_root/src/Bielu.AspNetCore.AsyncApi/Bielu.AspNetCore.AsyncApi.csproj" />
    <ProjectReference Include="$native_root/src/Bielu.AspNetCore.AsyncApi.Attributes/Bielu.AspNetCore.AsyncApi.Attributes.csproj" />
  </ItemGroup>
  <Import Project="$targets_dir/Bielu.AspNetCore.AsyncApi.ApiDescription.Server.targets" />
</Project>
EOF
cat > "$CONSUMER/Program.cs" <<'EOF'
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;

namespace Consumer;

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

public static class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(web => web
            .ConfigureServices(services => services.AddAsyncApi(options => options.WithInfo("Consumer", "1.0.0")))
            .Configure(app => app.UseRouting().UseEndpoints(endpoints => endpoints.MapAsyncApi())));
}
EOF
(
  cd "$CONSUMER"
  dotnet new tool-manifest >/dev/null
  dotnet tool install Bielu.AspNetCore.AsyncApi.Cli --version "$TOOL_VERSION" --add-source "$FEED" >/dev/null
  dotnet build -c Release -nologo -v q
)

for tfm in "${FRAMEWORKS[@]}"; do
  document="$CONSUMER/docs/$tfm/Consumer.json"
  if [ ! -s "$document" ] || ! grep -q '"asyncapi"' "$document" || ! grep -qi 'orderplaced' "$document"; then
    echo "::error::The $tfm build did not write $document. Found:" >&2
    find "$CONSUMER/docs" -type f >&2 || true
    exit 1
  fi
  echo "    docs/$tfm/Consumer.json OK"
done

echo "All CLI runtime combinations produce the document."
