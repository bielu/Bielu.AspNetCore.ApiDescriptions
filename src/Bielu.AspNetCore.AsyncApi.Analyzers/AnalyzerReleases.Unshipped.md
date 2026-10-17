; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Empty since the 1.0.0 baseline. Rules added, changed, or removed after 1.0.0 go here first;
; the release commit moves them under a new "## Release x.y.z" heading in
; AnalyzerReleases.Shipped.md.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
BASYNC010 | Usage | Warning | AddOpenApi() is called after AddAsyncApi() or AddArazzo() in the same method, so build-time generation may miss the AsyncAPI and Arazzo documents.
