// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that boots the target ASP.NET Core application, discovers
/// the IDocumentProvider service, and generates AsyncAPI documents.
/// </summary>
internal sealed class GetDocumentCommandWorker
{
    private const string DefaultDocumentName = "v1";
    private const string DocumentService = "Microsoft.Extensions.ApiDescriptions.IDocumentProvider";
    private const string DotString = ".";
    private const string InvalidFilenameString = "..";
    private const string JsonExtension = ".json";
    private const string UnderscoreString = "_";
    private static readonly char[] _invalidFilenameCharacters = Path.GetInvalidFileNameChars();
    private static readonly Encoding _utf8EncodingWithoutBOM
        = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const string GetDocumentsMethodName = "GetDocumentNames";
    private static readonly object[] _getDocumentsArguments = [];
    private static readonly Type[] _getDocumentsParameterTypes = Type.EmptyTypes;
    private static readonly Type _getDocumentsReturnType = typeof(IEnumerable<string>);

    private const string GenerateMethodName = "GenerateAsync";
    private static readonly Type[] _generateMethodParameterTypes = [typeof(string), typeof(TextWriter)];
    private static readonly Type _generateMethodReturnType = typeof(Task);

    private readonly GetDocumentCommandContext _context;
    private readonly Action<string> _writeInfo;
    private readonly Action<string> _writeWarning;
    private readonly Action<string> _writeError;

    public GetDocumentCommandWorker(
        GetDocumentCommandContext context,
        Action<string> writeInfo,
        Action<string> writeWarning,
        Action<string> writeError)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _writeInfo = writeInfo;
        _writeWarning = writeWarning;
        _writeError = writeError;
    }

    public int Process()
    {
        var assemblyName = new AssemblyName(_context.AssemblyName);
        var assembly = Assembly.Load(assemblyName);
        var entryPointType = assembly.EntryPoint?.DeclaringType;
        if (entryPointType == null)
        {
            _writeError($"Unable to find entry point in assembly '{_context.AssemblyPath}'.");
            return 3;
        }

        try
        {
            var services = ResolveServiceProvider(assembly, entryPointType, assemblyName);
            if (services == null)
            {
                _writeError("Unable to resolve the service provider from the host.");
                return 9;
            }

            var success = GetDocuments(services);
            if (!success)
            {
                return 10;
            }
        }
        catch (Exception ex)
        {
            _writeError(ex.ToString());
            return 11;
        }

        return 0;
    }

    /// <summary>
    /// Resolves the <see cref="IServiceProvider"/> from the target application by
    /// looking for well-known host builder patterns: CreateHostBuilder, CreateWebHostBuilder.
    /// </summary>
    /// <remarks>
    /// For minimal API / WebApplication patterns, use <c>Microsoft.Extensions.ApiDescription.Server</c>
    /// package instead, which has access to internal <c>HostFactoryResolver</c> APIs needed to
    /// properly boot the application.
    /// </remarks>
    private IServiceProvider? ResolveServiceProvider(Assembly assembly, Type entryPointType, AssemblyName assemblyName)
    {
        var args = new[] { $"--{HostDefaults.ApplicationKey}={assemblyName}" };

        // Try pattern: public static IHostBuilder CreateHostBuilder(string[] args)
        var createHostBuilder = entryPointType.GetMethod("CreateHostBuilder",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, [typeof(string[])], null);

        if (createHostBuilder != null && typeof(IHostBuilder).IsAssignableFrom(createHostBuilder.ReturnType))
        {
            _writeInfo("Found CreateHostBuilder method, using it to build the host.");
            var hostBuilder = (IHostBuilder)createHostBuilder.Invoke(null, [args])!;
            ConfigureHostBuilder(hostBuilder);
            var host = hostBuilder.Build();
            return host.Services;
        }

        // Try pattern: public static IHost BuildWebHost(string[] args)
        var buildWebHost = entryPointType.GetMethod("BuildWebHost",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, [typeof(string[])], null);

        if (buildWebHost != null)
        {
            _writeInfo("Found BuildWebHost method, using it to build the host.");
            var webHost = buildWebHost.Invoke(null, [args]);
            if (webHost != null)
            {
                var servicesProp = webHost.GetType().GetProperty("Services");
                if (servicesProp != null)
                {
                    return servicesProp.GetValue(webHost) as IServiceProvider;
                }
            }
        }

        // Try pattern: public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        var createWebHostBuilder = entryPointType.GetMethod("CreateWebHostBuilder",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, [typeof(string[])], null);

        if (createWebHostBuilder != null)
        {
            _writeInfo("Found CreateWebHostBuilder method, using it to build the host.");
            var webHostBuilder = createWebHostBuilder.Invoke(null, [args]);
            if (webHostBuilder != null)
            {
                var buildMethod = webHostBuilder.GetType().GetMethod("Build");
                if (buildMethod != null)
                {
                    var webHost = buildMethod.Invoke(webHostBuilder, null);
                    var servicesProp = webHost?.GetType().GetProperty("Services");
                    if (servicesProp != null)
                    {
                        return servicesProp.GetValue(webHost) as IServiceProvider;
                    }
                }
            }
        }

        _writeError("Unable to resolve host from the entry point. " +
                     "The CLI tool supports applications that expose a 'CreateHostBuilder(string[])', " +
                     "'CreateWebHostBuilder(string[])', or 'BuildWebHost(string[])' method. " +
                     "For minimal API / WebApplication patterns, use 'Microsoft.Extensions.ApiDescription.Server' " +
                     "package instead, which provides full support via the 'dotnet getdocument' tool.");
        return null;
    }

    private static void ConfigureHostBuilder(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddSingleton<IServer, NoopServer>();
            services.AddSingleton<IHostLifetime, NoopHostLifetime>();
        });
    }

    private bool GetDocuments(IServiceProvider services)
    {
        Type? serviceType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            serviceType = assembly.GetType(DocumentService, throwOnError: false);
            if (serviceType != null)
            {
                break;
            }
        }

        if (serviceType == null)
        {
            _writeError($"Unable to find type '{DocumentService}' in loaded assemblies. " +
                         "Ensure that the application references Bielu.AspNetCore.AsyncApi and calls AddAsyncApi().");
            return false;
        }

        var getDocumentsMethod = GetMethod(
            GetDocumentsMethodName,
            serviceType,
            _getDocumentsParameterTypes,
            _getDocumentsReturnType);
        if (getDocumentsMethod == null)
        {
            return false;
        }

        var generateMethod = GetMethod(
            GenerateMethodName,
            serviceType,
            _generateMethodParameterTypes,
            _generateMethodReturnType);
        if (generateMethod == null)
        {
            return false;
        }

        var service = services.GetService(serviceType);
        if (service == null)
        {
            _writeError($"Unable to resolve service '{DocumentService}' from the service provider. " +
                         "Ensure that AddAsyncApi() has been called during service registration.");
            return false;
        }

        // Get document names
        var documentNames = (IEnumerable<string>?)InvokeMethod(getDocumentsMethod, service, _getDocumentsArguments);
        if (documentNames == null)
        {
            return false;
        }

        // If an explicit document name is provided, generate only that document.
        if (!string.IsNullOrEmpty(_context.DocumentName))
        {
            if (!documentNames.Contains(_context.DocumentName))
            {
                _writeError($"Document '{_context.DocumentName}' was not found. " +
                             $"Available documents: {string.Join(", ", documentNames)}");
                return false;
            }

            documentNames = [_context.DocumentName];
        }

        if (!string.IsNullOrWhiteSpace(_context.FileName) && !Regex.IsMatch(_context.FileName, "^([A-Za-z0-9-_]+)$"))
        {
            _writeError("The file name format is invalid. Only alphanumeric characters, hyphens, and underscores are allowed.");
            return false;
        }

        // Write out the documents.
        var found = false;
        Directory.CreateDirectory(_context.OutputDirectory);
        var filePathList = new List<string>();
        foreach (var documentName in documentNames)
        {
            var filePath = GetDocument(
                documentName,
                _context.ProjectName,
                _context.OutputDirectory,
                generateMethod,
                service,
                _context.FileName);
            if (filePath == null)
            {
                return false;
            }

            filePathList.Add(filePath);
            found = true;
        }

        // Write out the cache file.
        if (!string.IsNullOrEmpty(_context.FileListPath))
        {
            var stream = File.Create(_context.FileListPath);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(string.Join(Environment.NewLine, filePathList));
        }

        if (!found)
        {
            _writeError("No AsyncAPI documents were found.");
        }

        return found;
    }

    private string? GetDocument(
        string documentName,
        string projectName,
        string outputDirectory,
        MethodInfo generateMethod,
        object service,
        string? fileName)
    {
        _writeInfo($"Generating AsyncAPI document '{documentName}'...");

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, _utf8EncodingWithoutBOM, bufferSize: 1024, leaveOpen: true))
        {
            object[] arguments = [documentName, writer];
            using var resultTask = (Task?)InvokeMethod(generateMethod, service, arguments);
            if (resultTask == null)
            {
                return null;
            }

            var finished = resultTask.Wait(TimeSpan.FromMinutes(1));
            if (!finished)
            {
                _writeError($"Method '{GenerateMethodName}' on '{DocumentService}' timed out after 1 minute.");
                return null;
            }
        }

        if (stream.Length == 0L)
        {
            _writeError(
                $"Method '{GenerateMethodName}' on '{DocumentService}' wrote no content for document '{documentName}'.");
            return null;
        }

        fileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : projectName;

        var filePath = GetDocumentPath(documentName, fileName, outputDirectory);
        _writeInfo($"Writing AsyncAPI document '{documentName}' to '{filePath}'.");
        try
        {
            stream.Position = 0L;

            using var outStream = File.Create(filePath);
            stream.CopyTo(outStream);
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }

        return filePath;
    }

    private static string GetDocumentPath(string documentName, string fileName, string outputDirectory)
    {
        string path;

        if (string.Equals(DefaultDocumentName, documentName, StringComparison.Ordinal))
        {
            // Leave default document name out of the filename.
            path = fileName + JsonExtension;
        }
        else
        {
            // Sanitize the document name because it may contain almost any character.
            var sanitizedDocumentName = string.Join(
                UnderscoreString,
                documentName.Split(_invalidFilenameCharacters));

            while (sanitizedDocumentName.Contains(InvalidFilenameString, StringComparison.Ordinal))
            {
                sanitizedDocumentName = sanitizedDocumentName.Replace(
                    InvalidFilenameString,
                    DotString,
                    StringComparison.Ordinal);
            }

            path = $"{fileName}_{sanitizedDocumentName}{JsonExtension}";
        }

        return Path.Combine(outputDirectory, path);
    }

    private MethodInfo? GetMethod(string methodName, Type type, Type[] parameterTypes, Type returnType)
    {
        var method = type.GetMethod(methodName, parameterTypes);
        if (method == null)
        {
            _writeError($"Method '{methodName}' was not found on type '{type}'.");
            return null;
        }

        if (method.IsStatic)
        {
            _writeWarning($"Method '{methodName}' on type '{type}' should not be static.");
            return null;
        }

        if (!returnType.IsAssignableFrom(method.ReturnType))
        {
            _writeWarning($"Method '{methodName}' on type '{type}' has unsupported return type '{method.ReturnType}'. Expected '{returnType}'.");
            return null;
        }

        return method;
    }

    private object? InvokeMethod(MethodInfo method, object instance, object[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (Exception ex)
        {
            _writeError($"Error invoking method '{method.Name}': {ex}");
            return null;
        }
    }
}
