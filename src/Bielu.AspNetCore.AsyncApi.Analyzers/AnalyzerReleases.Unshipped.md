; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
BASYNC001 | Usage | Warning | A type carrying AsyncAPI attributes is missing [AsyncApi], so the scanner ignores it.
BASYNC002 | Usage | Warning | An operation attribute has no [Channel] on the method or its containing type.
BASYNC003 | Usage | Warning | Duplicate Name across AllowMultiple [Message] or [ChannelParameter] attributes.
BASYNC004 | Usage | Info | An [AsyncApi("name")] document name never appears in an AddAsyncApi("name", ...) call.
BASYNC005 | Usage | Warning | ChannelParameter.Type or Message.PayloadType cannot be used for schema generation.
BASYNC006 | Usage | Error | [MessageExample] declares a Json literal that is not valid JSON.
BASYNC007 | Usage | Warning | An IAsyncApiMessageExampleProvider implementation has no public parameterless constructor.
BASYNC008 | Usage | Warning | A MessageId or Name contains characters the AsyncAPI specification discourages.
BASYNC009 | Usage | Info | A public [AsyncApi] component has neither Summary nor Description.
