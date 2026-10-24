; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; The generator shares the BASYNC rule ids with Bielu.AspNetCore.AsyncApi.Analyzers, so check both
; projects before taking the next id.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
BASYNC011 | Usage | Warning | A custom operation attribute is not accessible from the generated metadata provider, so its operation is left out.
