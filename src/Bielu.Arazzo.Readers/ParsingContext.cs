namespace Bielu.Arazzo.Readers;

internal sealed class ParsingContext
{
    public ParsingContext(ArazzoReaderSettings settings, ArazzoDiagnostics diagnostics)
    {
        Settings = settings;
        Diagnostics = diagnostics;
    }

    public ArazzoReaderSettings Settings { get; }

    public ArazzoDiagnostics Diagnostics { get; }

    public void Error(string path, string message) => Diagnostics.Errors.Add(new ArazzoReaderError(path, message));

    public void Warn(string path, string message) => Diagnostics.Warnings.Add(new ArazzoReaderError(path, message));
}
