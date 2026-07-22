// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

internal enum ChangeSeverity
{
    Breaking,
    NonBreaking
}

internal record AsyncApiChange(string Path, string Message, ChangeSeverity Severity);

internal sealed class AsyncApiDocumentComparer
{
    public IEnumerable<AsyncApiChange> Compare(AsyncApiDocument @base, AsyncApiDocument head)
    {
        var changes = new List<AsyncApiChange>();

        // Compare Servers
        CompareDictionaries(@base.Servers, head.Servers, "servers", "Server", changes);

        // Compare Channels
        CompareChannels(@base.Channels, head.Channels, changes);

        // Compare Operations
        CompareOperations(@base.Operations, head.Operations, changes);

        // Compare Messages in Components
        CompareMessages(@base.Components?.Messages, head.Components?.Messages, "components/messages", changes);

        // Compare Schemas in Components
        CompareSchemas(@base.Components?.Schemas, head.Components?.Schemas, "components/schemas", changes);

        return changes;
    }

    private void CompareDictionaries<T>(IDictionary<string, T>? @base, IDictionary<string, T>? head, string path, string typeName, List<AsyncApiChange> changes)
    {
        @base ??= new Dictionary<string, T>();
        head ??= new Dictionary<string, T>();

        foreach (var key in @base.Keys)
        {
            if (!head.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"{typeName} '{key}' was removed.", ChangeSeverity.Breaking));
            }
        }

        foreach (var key in head.Keys)
        {
            if (!@base.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"{typeName} '{key}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private void CompareChannels(IDictionary<string, AsyncApiChannel>? @base, IDictionary<string, AsyncApiChannel>? head, List<AsyncApiChange> changes)
    {
        @base ??= new Dictionary<string, AsyncApiChannel>();
        head ??= new Dictionary<string, AsyncApiChannel>();

        foreach (var key in @base.Keys)
        {
            if (!head.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"channels/{key}", $"Channel '{key}' was removed.", ChangeSeverity.Breaking));
                continue;
            }

            var bChannel = @base[key];
            var hChannel = head[key];

            if (bChannel.Address != hChannel.Address)
            {
                changes.Add(new AsyncApiChange($"channels/{key}/address", $"Channel '{key}' address changed from '{bChannel.Address}' to '{hChannel.Address}'.", ChangeSeverity.Breaking));
            }
        }

        foreach (var key in head.Keys)
        {
            if (!@base.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"channels/{key}", $"Channel '{key}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private void CompareOperations(IDictionary<string, AsyncApiOperation>? @base, IDictionary<string, AsyncApiOperation>? head, List<AsyncApiChange> changes)
    {
        @base ??= new Dictionary<string, AsyncApiOperation>();
        head ??= new Dictionary<string, AsyncApiOperation>();

        foreach (var key in @base.Keys)
        {
            if (!head.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"operations/{key}", $"Operation '{key}' was removed.", ChangeSeverity.Breaking));
                continue;
            }

            var bOp = @base[key];
            var hOp = head[key];

            if (bOp.Action != hOp.Action)
            {
                changes.Add(new AsyncApiChange($"operations/{key}/action", $"Operation '{key}' action changed from '{bOp.Action}' to '{hOp.Action}'.", ChangeSeverity.Breaking));
            }

            // Simplified: compare channel references
            var bChannelRef = bOp.Channel?.Reference;
            var hChannelRef = hOp.Channel?.Reference;
            if (bChannelRef != hChannelRef)
            {
                changes.Add(new AsyncApiChange($"operations/{key}/channel", $"Operation '{key}' channel reference changed.", ChangeSeverity.Breaking));
            }
        }

        foreach (var key in head.Keys)
        {
            if (!@base.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"operations/{key}", $"Operation '{key}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private void CompareMessages(IDictionary<string, AsyncApiMessage>? @base, IDictionary<string, AsyncApiMessage>? head, string path, List<AsyncApiChange> changes)
    {
        @base ??= new Dictionary<string, AsyncApiMessage>();
        head ??= new Dictionary<string, AsyncApiMessage>();

        foreach (var key in @base.Keys)
        {
            if (!head.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"Message '{key}' was removed.", ChangeSeverity.Breaking));
            }
        }

        foreach (var key in head.Keys)
        {
            if (!@base.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"Message '{key}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private void CompareSchemas(IDictionary<string, AsyncApiMultiFormatSchema>? @base, IDictionary<string, AsyncApiMultiFormatSchema>? head, string path, List<AsyncApiChange> changes)
    {
        @base ??= new Dictionary<string, AsyncApiMultiFormatSchema>();
        head ??= new Dictionary<string, AsyncApiMultiFormatSchema>();

        foreach (var key in @base.Keys)
        {
            if (!head.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"Schema '{key}' was removed.", ChangeSeverity.Breaking));
                continue;
            }

            // Deep comparison of schemas is complex, for MVP let's just check for removal.
            // Ideally we should check for narrowing changes here.
        }

        foreach (var key in head.Keys)
        {
            if (!@base.ContainsKey(key))
            {
                changes.Add(new AsyncApiChange($"{path}/{key}", $"Schema '{key}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }
}
