#!/usr/bin/env bash
#
# Verifies build-time document generation end to end with Microsoft's own `dotnet getdocument` tool, which is the
# one real consumers use and the one unit tests cannot run.
#
# src/examples/BuildTimeDocuments registers AddOpenApi(), AddAsyncApi() and AddArazzo() with the same default
# document name, `v1`. Three things are checked:
#   1. one build writes all three documents, under three distinct file names (OpenAPI keeps `v1`; AsyncAPI and
#      Arazzo get a prefix because the name is taken);
#   2. each file is the right kind of document and passes its own validator; and
#   3. a stale Arazzo reference (a channel the app does not have) fails the build instead of shipping a workflow
#      that points at nothing. The tool boots the app, so Arazzo's startup validation runs, and the build has to
#      surface it.
#
# Usage: scripts/verify-build-time-documents.sh
set -euo pipefail

PROJECT="src/examples/BuildTimeDocuments"
NAME="BuildTimeDocuments"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

echo "==> Building $PROJECT and writing its documents"
dotnet build "$PROJECT" -c Release -t:Rebuild -p:VerifyBuildTimeDocuments=true -p:OpenApiDocumentsDirectory="$WORK/docs"

expect_file() {
  local file="$1" marker="$2"
  if [ ! -s "$WORK/docs/$file" ]; then
    echo "::error::$file was not written. Found:" >&2
    ls -1 "$WORK/docs" >&2 || true
    exit 1
  fi
  if ! grep -q "$marker" "$WORK/docs/$file"; then
    echo "::error::$file does not look like the expected document (no $marker)" >&2
    exit 1
  fi
  echo "    $file OK"
}

echo "==> Checking the generated files"
expect_file "$NAME.json" '"openapi"'
expect_file "${NAME}_asyncapi-v1.json" '"asyncapi"'
expect_file "${NAME}_arazzo-v1.json" '"arazzo"'

unexpected="$(ls -1 "$WORK/docs" | grep -v -x -e "$NAME.json" -e "${NAME}_asyncapi-v1.json" -e "${NAME}_arazzo-v1.json" || true)"
if [ -n "$unexpected" ]; then
  echo "::error::Unexpected files were written:" >&2
  echo "$unexpected" >&2
  exit 1
fi

echo "==> Validating the Arazzo and AsyncAPI documents with their CLIs"
dotnet run --project src/Bielu.Arazzo.Cli -c Release -- validate --file "$WORK/docs/${NAME}_arazzo-v1.json"
dotnet run --project src/Bielu.AspNetCore.AsyncApi.Cli -c Release -- validate --file "$WORK/docs/${NAME}_asyncapi-v1.json"

echo "==> A stale Arazzo reference must fail the build"
if BUILD_TIME_DOCUMENTS_BREAK_ARAZZO=1 dotnet build "$PROJECT" -c Release -t:Rebuild \
    -p:VerifyBuildTimeDocuments=true -p:OpenApiDocumentsDirectory="$WORK/broken" >"$WORK/broken.log" 2>&1; then
  echo "::error::The build succeeded although the Arazzo workflow references a channel that does not exist" >&2
  exit 1
fi
if ! grep -q "doesNotExist" "$WORK/broken.log"; then
  echo "::error::The build failed, but not because of the stale reference:" >&2
  tail -n 40 "$WORK/broken.log" >&2
  exit 1
fi
echo "    build failed on the stale reference, as it should"

echo "Build-time document generation verified."
