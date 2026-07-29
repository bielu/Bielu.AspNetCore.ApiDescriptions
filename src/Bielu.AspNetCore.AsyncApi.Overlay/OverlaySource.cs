// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Overlay.Models;
using Bielu.Overlay.Readers;

namespace Bielu.AspNetCore.Overlay;

/// <summary>
/// Where an <see cref="OverlayDocument"/> comes from. Resolution is deferred so a file is not read while
/// the service collection is being configured, and cached so it is read once rather than per request.
/// </summary>
public abstract class OverlaySource
{
    /// <summary>A human-readable description of this source, used in error messages.</summary>
    public abstract string Origin { get; }

    /// <summary>Returns the overlay document, loading it on first use.</summary>
    /// <exception cref="OverlayApplicationException">The overlay could not be read or is not valid.</exception>
    public abstract OverlayDocument Resolve();

    /// <summary>Creates a source that reads an overlay from a JSON or YAML file.</summary>
    /// <param name="path">Path to the overlay file. Relative paths resolve against the process working directory.</param>
    public static OverlaySource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileOverlaySource(path);
    }

    /// <summary>Creates a source wrapping an already-constructed overlay document.</summary>
    /// <param name="document">The overlay to apply.</param>
    public static OverlaySource FromDocument(OverlayDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new DocumentOverlaySource(document);
    }

    private sealed class DocumentOverlaySource(OverlayDocument document) : OverlaySource
    {
        public override string Origin => "in-memory overlay";

        public override OverlayDocument Resolve() => document;
    }

    private sealed class FileOverlaySource : OverlaySource
    {
        private readonly Lazy<OverlayDocument> _document;

        public FileOverlaySource(string path)
        {
            Origin = path;

            // ExecutionAndPublication: a document endpoint can be hit concurrently the moment the app
            // starts, and reading the file once is the point of caching it.
            _document = new Lazy<OverlayDocument>(() => Load(path), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public override string Origin { get; }

        public override OverlayDocument Resolve() => _document.Value;

        private static OverlayDocument Load(string path)
        {
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // File.Exists would not have helped here: a file can exist and still be locked,
                // unreadable, or on a path this process cannot traverse.
                throw new OverlayApplicationException($"Failed to read overlay '{path}': {ex.Message}", ex);
            }

            var read = OverlayStringReader.Read(content);

            if (read.HasErrors || read.Document is null)
            {
                var detail = string.Join("; ", read.Diagnostics.Where(d => !d.IsWarning).Select(d => $"{d.Path}: {d.Message}"));
                throw new OverlayApplicationException(
                    $"Overlay '{path}' is not valid: {(detail.Length == 0 ? "the document could not be read." : detail)}");
            }

            return read.Document;
        }
    }
}
