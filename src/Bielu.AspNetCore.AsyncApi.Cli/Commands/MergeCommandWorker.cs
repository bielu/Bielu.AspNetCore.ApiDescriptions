// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that merges multiple AsyncAPI documents into a single document.
/// </summary>
internal sealed class MergeCommandWorker
{
    private static readonly Encoding _utf8EncodingWithoutBOM
        = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly MergeCommandContext _context;
    private readonly Action<string> _writeInfo;
    private readonly Action<string> _writeError;

    public MergeCommandWorker(
        MergeCommandContext context,
        Action<string> writeInfo,
        Action<string> writeError)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writeInfo);
        ArgumentNullException.ThrowIfNull(writeError);

        _context = context;
        _writeInfo = writeInfo;
        _writeError = writeError;
    }

    public int Process()
    {
        try
        {
            var options = new AsyncApiMergeOptions();

            if (_context.Title is not null || _context.Version is not null)
            {
                options.Info = new AsyncApiInfo
                {
                    Title = _context.Title ?? "Merged AsyncAPI",
                    Version = _context.Version ?? "1.0.0"
                };
            }

            for (var i = 0; i < _context.Sources.Count; i++)
            {
                var prefix = i < _context.Prefixes.Count ? _context.Prefixes[i] : null;
                options.AddSource(_context.Sources[i], prefix);
            }

            _writeInfo($"Merging {options.Sources.Count} AsyncAPI document(s)...");

            using var httpClient = new HttpClient { Timeout = options.HttpTimeout };
            var merger = new AsyncApiDocumentMerger(httpClient);
            var merged = merger.MergeAsync(options).GetAwaiter().GetResult();

            var isYaml = _context.OutputPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                         _context.OutputPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

            string serialized;
            if (isYaml)
            {
                serialized = AsyncApiSerializationHelper.SerializeV3ToYaml(merged);
            }
            else
            {
                serialized = AsyncApiSerializationHelper.SerializeV3ToJson(merged);
            }

            var directory = Path.GetDirectoryName(_context.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_context.OutputPath, serialized, _utf8EncodingWithoutBOM);
            _writeInfo($"Merged document written to '{_context.OutputPath}'.");

            return 0;
        }
        catch (Exception ex)
        {
            _writeError(ex.Message);
            return 1;
        }
    }
}
