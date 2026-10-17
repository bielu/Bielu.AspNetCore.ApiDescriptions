; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Rules added, changed, or removed since the last release go here first; the release commit moves
; them under a new "## Release x.y.z" heading in AnalyzerReleases.Shipped.md.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
BARAZZO001 | Usage | Warning | AddOpenApi() is called after AddArazzo() in the same method, so build-time generation may miss the Arazzo documents.
BARAZZO002 | Usage | Error | A literal workflow, step or source-description id, or a marker type's derived id, is not a valid Arazzo identifier.
BARAZZO003 | Usage | Error | A step sets no target, or more than one, of Operation/OperationPath/Channel/Workflow.
BARAZZO004 | Usage | Error | A Channel step's action is neither "send" nor "receive".
BARAZZO005 | Usage | Warning | WithCorrelationId is set on a step whose action isn't "receive".
BARAZZO006 | Usage | Warning | A step references a source description the document never declares.
BARAZZO007 | Usage | Warning | DependsOn or Workflow references a step or workflow the document never defines.
BARAZZO008 | Usage | Info | AddAsyncApiSource or AddOpenApiSource points at a document the project never registers.
