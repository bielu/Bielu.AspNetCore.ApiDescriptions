// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;

// Parse command line arguments
// Usage: dotnet asyncapi getdocument --assembly <name> --assembly-path <path> --output <dir> --project <name> [--document <name>] [--file-list <path>] [--file-name <name>]
// Usage: dotnet asyncapi merge --source <uri> [--source <uri> ...] --output <path> [--prefix <prefix> ...] [--title <title>] [--version <version>]

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    PrintUsage();
    return 0;
}

if (args[0] == "merge")
{
    return RunMerge(args);
}

if (args[0] != "getdocument")
{
    Console.Error.WriteLine($"Unknown command: {args[0]}");
    PrintUsage();
    return 1;
}

var context = new GetDocumentCommandContext();
var additionalProbingPaths = new List<string>();
var additionalDeps = new List<string>();

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--assembly":
            context.AssemblyName = args[++i];
            break;
        case "--assembly-path":
            context.AssemblyPath = args[++i];
            break;
        case "--output":
            context.OutputDirectory = args[++i];
            break;
        case "--project":
            context.ProjectName = args[++i];
            break;
        case "--document":
            context.DocumentName = args[++i];
            break;
        case "--file-list":
            context.FileListPath = args[++i];
            break;
        case "--file-name":
            context.FileName = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

// Validate required arguments
if (string.IsNullOrEmpty(context.AssemblyName))
{
    Console.Error.WriteLine("Error: --assembly is required.");
    return 1;
}
if (string.IsNullOrEmpty(context.OutputDirectory))
{
    Console.Error.WriteLine("Error: --output is required.");
    return 1;
}
if (string.IsNullOrEmpty(context.ProjectName))
{
    context.ProjectName = context.AssemblyName;
}

// Set up assembly resolution for the target application
if (!string.IsNullOrEmpty(context.AssemblyPath))
{
    var directory = Path.GetDirectoryName(context.AssemblyPath);
    if (directory != null)
    {
        // Add the assembly's directory to the resolution path
        AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
        {
            var name = new System.Reflection.AssemblyName(resolveArgs.Name);
            var candidatePath = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(candidatePath))
            {
                return System.Reflection.Assembly.LoadFrom(candidatePath);
            }
            return null;
        };
    }
}

var worker = new GetDocumentCommandWorker(
    context,
    writeInfo: msg => Console.WriteLine($"info: {msg}"),
    writeWarning: msg => Console.WriteLine($"warn: {msg}"),
    writeError: msg => Console.Error.WriteLine($"error: {msg}"));

return worker.Process();

static int RunMerge(string[] args)
{
    var mergeContext = new MergeCommandContext();

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--source":
                if (++i >= args.Length) { Console.Error.WriteLine("Error: --source requires a value."); return 1; }
                mergeContext.Sources.Add(args[i]);
                break;
            case "--output":
                if (++i >= args.Length) { Console.Error.WriteLine("Error: --output requires a value."); return 1; }
                mergeContext.OutputPath = args[i];
                break;
            case "--prefix":
                if (++i >= args.Length) { Console.Error.WriteLine("Error: --prefix requires a value."); return 1; }
                mergeContext.Prefixes.Add(args[i]);
                break;
            case "--title":
                if (++i >= args.Length) { Console.Error.WriteLine("Error: --title requires a value."); return 1; }
                mergeContext.Title = args[i];
                break;
            case "--version":
                if (++i >= args.Length) { Console.Error.WriteLine("Error: --version requires a value."); return 1; }
                mergeContext.Version = args[i];
                break;
            default:
                Console.Error.WriteLine($"Unknown argument for merge: {args[i]}");
                PrintUsage();
                return 1;
        }
    }

    if (mergeContext.Sources.Count == 0)
    {
        Console.Error.WriteLine("Error: at least one --source is required for merge.");
        return 1;
    }

    if (string.IsNullOrEmpty(mergeContext.OutputPath))
    {
        Console.Error.WriteLine("Error: --output is required for merge.");
        return 1;
    }

    var mergeWorker = new MergeCommandWorker(
        mergeContext,
        writeInfo: msg => Console.WriteLine($"info: {msg}"),
        writeError: msg => Console.Error.WriteLine($"error: {msg}"));

    return mergeWorker.Process();
}

static void PrintUsage()
{
    Console.WriteLine("Bielu.AspNetCore.AsyncApi CLI - Generate and merge AsyncAPI documents");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet asyncapi <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  getdocument    Generate AsyncAPI documents from ASP.NET Core applications");
    Console.WriteLine("  merge          Merge multiple AsyncAPI documents into one");
    Console.WriteLine();
    Console.WriteLine("getdocument options:");
    Console.WriteLine("  --assembly <name>        The assembly name to load (required)");
    Console.WriteLine("  --assembly-path <path>   The full path to the assembly");
    Console.WriteLine("  --output <dir>           The output directory for generated documents (required)");
    Console.WriteLine("  --project <name>         The project name (used for file naming)");
    Console.WriteLine("  --document <name>        Generate only the specified document");
    Console.WriteLine("  --file-list <path>       Path to write the list of generated files");
    Console.WriteLine("  --file-name <name>       Override file name (without extension)");
    Console.WriteLine();
    Console.WriteLine("merge options:");
    Console.WriteLine("  --source <uri>           A document source URI - file path or URL (required, repeatable)");
    Console.WriteLine("  --output <path>          The output file path for the merged document (required)");
    Console.WriteLine("  --prefix <prefix>        Key prefix for the corresponding source (optional, repeatable)");
    Console.WriteLine("  --title <title>          Title for the merged document");
    Console.WriteLine("  --version <version>      Version for the merged document");
    Console.WriteLine();
    Console.WriteLine("  -h, --help               Show this help message");
}
