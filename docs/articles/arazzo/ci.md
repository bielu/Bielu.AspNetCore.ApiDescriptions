# Keeping workflows in step with the code

An Arazzo workflow names operations and channels of the APIs it calls. When code renames an operation, removes a
channel or changes a payload, the workflow goes stale without anything failing on its own. This page shows how to
catch that at each stage, from the editor to a pull-request report, and which tool does what.

| Stage | Tool | Catches | Needs |
|-------|------|---------|-------|
| Editor and `dotnet build` | Analyzers BARAZZO006–BARAZZO009 | Undeclared sources, unknown step or workflow ids, a `Channel` step naming a channel no `[Channel]` declares | Nothing, they ship with `Bielu.AspNetCore.Arazzo` |
| App startup | `ValidateSourceReferencesOnStartup` (on by default) | Any step whose operation or channel isn't in the app's live OpenAPI/AsyncAPI documents | The app to start |
| `dotnet build` with build-time generation | `dotnet getdocument` boots the app, so startup validation fails the build | Same as startup | [Build-time generation](../build-time-generation.md) |
| CI, from files | `dotnet arazzo validate --source` | Same check against OpenAPI/AsyncAPI files, without starting the app | The documents as files |
| Pull request | `dotnet arazzo impact` | Which steps a change breaks, and which ones point at an operation or channel whose definition changed | The documents before and after the change |

The analyzers and startup validation work with no setup. The rest of this page is about CI.

## 1. Fail the build on a stale workflow

If the Arazzo document is built in code (`AddArazzo`) in the same app as the APIs it calls, turn on build-time
generation. The build then starts the app, startup validation runs, and a step pointing at a renamed operation
fails `dotnet build`:

```xml
<PropertyGroup>
    <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
    <OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/docs</OpenApiDocumentsDirectory>
</PropertyGroup>
```

```text
error : v1:placeOrder.announce: channelPath '{$sourceDescriptions.events.url}#/channels/orderPlaced' did not resolve.
```

To make BARAZZO009 fail the build too, rather than only warn in the editor:

```xml
<PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);BARAZZO009</WarningsAsErrors>
</PropertyGroup>
```

For a channel added at runtime, such as one from Wolverine routing, mark the step with `dynamic: true`. The
analyzer then skips it, and startup validation still checks it (see [Analyzers](overview.md#analyzers)).

## 2. Commit the generated documents

With `OpenApiDocumentsDirectory` inside the repository, commit the generated files. A pull request that changes an
operation, a channel or a workflow then shows the change in the documents as part of the diff. A CI step makes
sure nobody forgets to commit them:

```yaml
- run: dotnet build src/MyApi
- run: git diff --exit-code -- src/MyApi/docs
```

## 3. Check hand-written or external workflows

When the Arazzo document is a file rather than code, startup validation never sees it. That covers a
`checkout.arazzo.yaml` kept next to the API, and a workflow repository that calls services owned by other teams.
Give `dotnet arazzo validate` the OpenAPI/AsyncAPI documents its sources point at:

```yaml
- run: dotnet tool install -g Bielu.Arazzo.Cli
- run: |
    dotnet arazzo validate --file api/checkout.arazzo.yaml \
      --source shop=src/MyApi/docs/MyApi.json \
      --source events=src/MyApi/docs/MyApi_asyncapi-v1.json
```

If the source descriptions' `url`s are relative paths to those files, `--resolve-local-sources` finds them
without naming each one. For services owned by other teams, download their published documents first. A
scheduled run catches the day a provider changes its API:

```yaml
on:
  schedule: [{ cron: "0 6 * * *" }]
jobs:
  workflows:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - run: dotnet tool install -g Bielu.Arazzo.Cli
      - run: |
          curl -fsSL https://payments.example.com/openapi/v1.json -o payments.json
          curl -fsSL https://orders.example.com/asyncapi/v1.json -o orders.json
          dotnet arazzo validate --file workflows/*.arazzo.yaml --source payments=payments.json --source orders=orders.json
```

A declared source you don't pass is skipped and listed in the output, so a partial check still runs.

## 4. Report the affected workflows on a pull request

`dotnet arazzo impact` compares the documents before and after a change and lists the workflow steps it
affects. A step whose operation or channel disappeared is **breaking**. A step whose target changed (its
parameters, request body, responses, messages or payload schemas) is listed for review. The workflow below
builds the app on the pull request's base commit and on its head, then posts the report as a comment.

One setting is needed first. A pull request that breaks a workflow also fails the head build, because startup
validation runs in it (section 1), and then no documents are written to compare. Let the impact job turn the check
off through configuration:

```csharp
builder.Services.AddArazzo(options =>
{
    options.ValidateSourceReferencesOnStartup = builder.Configuration.GetValue("Arazzo:ValidateOnStartup", true);
    // ...
});
```

```yaml
name: Arazzo impact
on: pull_request

permissions:
  contents: read
  pull-requests: write

jobs:
  impact:
    runs-on: ubuntu-latest
    env:
      # The impact report checks the references, so the build doesn't have to fail on them.
      Arazzo__ValidateOnStartup: "false"
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - run: dotnet tool install -g Bielu.Arazzo.Cli

      - name: Documents after the change
        run: dotnet build src/MyApi -p:OpenApiDocumentsDirectory=$RUNNER_TEMP/head

      - name: Documents before the change
        run: |
          git worktree add "$RUNNER_TEMP/base-src" "${{ github.event.pull_request.base.sha }}"
          dotnet build "$RUNNER_TEMP/base-src/src/MyApi" -p:OpenApiDocumentsDirectory=$RUNNER_TEMP/base

      - name: Affected workflows
        run: |
          dotnet arazzo impact --file "$RUNNER_TEMP/head/MyApi_arazzo-v1.json" \
            --base shop="$RUNNER_TEMP/base/MyApi.json" --base events="$RUNNER_TEMP/base/MyApi_asyncapi-v1.json" \
            --head shop="$RUNNER_TEMP/head/MyApi.json" --head events="$RUNNER_TEMP/head/MyApi_asyncapi-v1.json" \
            --format markdown --fail-on-breaking > impact.md

      - name: Comment on the pull request
        if: always()
        env:
          GH_TOKEN: ${{ github.token }}
        run: gh pr comment ${{ github.event.pull_request.number }} --body-file impact.md --edit-last --create-if-none
```

Replace `shop` and `events` with your source-description names (the first argument of `AddOpenApiSource` and
`AddAsyncApiSource`), and the file names with the ones your build writes (see
[Build-time generation](../build-time-generation.md#arazzo-documents)).

The report uses the head commit's Arazzo document. A pull request that renames an operation and updates the
workflow in the same change shows the step as resolving again, not as breaking, so fixing the workflow in the same
pull request is what makes the job pass. Keep the regular build from section 1 in another job, without
`Arazzo__ValidateOnStartup`, so the check still gates the merge.

The comment looks like this:

```markdown
### Arazzo Workflow Impact Report

#### Breaking Changes

| Path | Change |
| --- | --- |
| `placeOrder.announce` | channelPath '{$sourceDescriptions.events.url}#/channels/orderPlaced' did not resolve. It resolved before the change. |
```

## Exit codes

Every command exits with `0` on success and `1` on failure, so any CI system can use them:

- `validate` exits `1` on an error, including a step reference that doesn't resolve, and with `--strict` on a
  warning too.
- `impact` exits `1` on a breaking change only with `--fail-on-breaking`. Without it, the report is informational.
- Both exit `1` when a given file can't be read or parsed.

See the [CLI Tool](cli.md) article for every option.
