using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.AsyncApi.Services.XmlDocs;

internal class XmlDocumentationProvider(ILogger<XmlDocumentationProvider> logger)
{
    private readonly ConcurrentDictionary<string, XmlDocumentation> _cache = new();
    private readonly ConcurrentDictionary<string, bool> _loadedFiles = new();

    public void Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !_loadedFiles.TryAdd(filePath, true))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            logger.LogWarning("XML documentation file not found: {FilePath}", filePath);
            return;
        }

        try
        {
            var document = XDocument.Load(filePath);
            foreach (var element in document.XPathSelectElements("/doc/members/member"))
            {
                var name = element.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name)) continue;

                var summary = element.Element("summary")?.Value.Trim();
                var remarks = element.Element("remarks")?.Value.Trim();
                var parameters = element.Elements("param")
                    .ToDictionary(
                        x => x.Attribute("name")?.Value ?? string.Empty,
                        x => x.Value.Trim());

                _cache[name] = new XmlDocumentation(summary, remarks, parameters.Count > 0 ? parameters : null);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load XML documentation file: {FilePath}", filePath);
            _loadedFiles.TryRemove(filePath, out _);
        }
    }

    public XmlDocumentation? GetDocumentation(MemberInfo memberInfo)
    {
        var id = XmlDocumentationIdBuilder.CreateIdForMember(memberInfo);
        if (id != null && _cache.TryGetValue(id, out var documentation))
        {
            return documentation;
        }

        return null;
    }
}

internal record XmlDocumentation(string? Summary, string? Remarks, Dictionary<string, string>? Parameters);
