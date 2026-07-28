namespace Bielu.Overlay.Readers;

internal sealed class ParsingContext
{
    public ParsingContext(OverlayReaderSettings settings, List<OverlayDiagnostic> diagnostics)
    {
        Settings = settings;
        Diagnostics = diagnostics;
    }

    public OverlayReaderSettings Settings { get; }

    public List<OverlayDiagnostic> Diagnostics { get; }

    public void Error(string path, string message) => Diagnostics.Add(new OverlayDiagnostic(path, message));

    public void Warn(string path, string message) => Diagnostics.Add(new OverlayDiagnostic(path, message, isWarning: true));
}
