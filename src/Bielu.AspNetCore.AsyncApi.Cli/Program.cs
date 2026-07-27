// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.Cli.Shared;

// Parse command line arguments
// Usage: dotnet asyncapi getdocument --assembly <name> --assembly-path <path> --output <dir> --project <name> [--document <name>] [--file-list <path>] [--file-name <name>]
// Usage: dotnet asyncapi merge --source <uri> [--source <uri> ...] --output <path> [--prefix <prefix> ...] [--title <title>] [--version <version>]

var logger = new ConsoleCliLogger();

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    PrintUsage();
    return CliExitCode.Success;
}

if (args[0] == "merge")
{
    return RunMerge(args);
}

if (args[0] == "validate")
{
    return RunValidate(args);
}

if (args[0] == "diff")
{
    return RunDiff(args);
}

if (args[0] != "getdocument")
{
    logger.Error($"Unknown command: {args[0]}");
    PrintUsage();
    return CliExitCode.Failure;
}

var context = new GetDocumentCommandContext();

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
            logger.Error($"Unknown argument: {args[i]}");
            PrintUsage();
            return CliExitCode.Failure;
    }
}

// Validate required arguments
if (string.IsNullOrEmpty(context.AssemblyName))
{
    logger.Error("--assembly is required.");
    return CliExitCode.Failure;
}

if (string.IsNullOrEmpty(context.OutputDirectory))
{
    logger.Error("--output is required.");
    return CliExitCode.Failure;
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
        AppDomain.CurrentDomain.AssemblyResolve += (_, resolveArgs) =>
        {
            var name = new AssemblyName(resolveArgs.Name);
            var candidatePath = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(candidatePath))
            {
                return Assembly.LoadFrom(candidatePath);
            }

            return null;
        };
    }
}

var worker = new GetDocumentCommandWorker(
    context,
    writeInfo: logger.Info,
    writeWarning: logger.Warning,
    writeError: logger.Error);

return worker.Process();

int RunMerge(string[] mergeArgs)
{
    var mergeContext = new MergeCommandContext();

    for (var i = 1; i < mergeArgs.Length; i++)
    {
        switch (mergeArgs[i])
        {
            case "--source":
                if (!CliArgumentReader.TryReadValue(mergeArgs, ref i, "--source", logger, out var source))
                {
                    return CliExitCode.Failure;
                }

                mergeContext.Sources.Add(source);
                break;
            case "--output":
                if (!CliArgumentReader.TryReadValue(mergeArgs, ref i, "--output", logger, out var output))
                {
                    return CliExitCode.Failure;
                }

                mergeContext.OutputPath = output;
                break;
            case "--prefix":
                if (!CliArgumentReader.TryReadValue(mergeArgs, ref i, "--prefix", logger, out var prefix))
                {
                    return CliExitCode.Failure;
                }

                mergeContext.Prefixes.Add(prefix);
                break;
            case "--title":
                if (!CliArgumentReader.TryReadValue(mergeArgs, ref i, "--title", logger, out var title))
                {
                    return CliExitCode.Failure;
                }

                mergeContext.Title = title;
                break;
            case "--version":
                if (!CliArgumentReader.TryReadValue(mergeArgs, ref i, "--version", logger, out var version))
                {
                    return CliExitCode.Failure;
                }

                mergeContext.Version = version;
                break;
            default:
                logger.Error($"Unknown argument for merge: {mergeArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (mergeContext.Sources.Count == 0)
    {
        logger.Error("At least one --source is required for merge.");
        return CliExitCode.Failure;
    }

    if (string.IsNullOrEmpty(mergeContext.OutputPath))
    {
        logger.Error("--output is required for merge.");
        return CliExitCode.Failure;
    }

    var mergeWorker = new MergeCommandWorker(
        mergeContext,
        writeInfo: logger.Info,
        writeError: logger.Error);

    return mergeWorker.Process();
}

int RunValidate(string[] validateArgs)
{
    var validateContext = new ValidateCommandContext();

    for (var i = 1; i < validateArgs.Length; i++)
    {
        switch (validateArgs[i])
        {
            case "--file":
                if (!CliArgumentReader.TryReadValue(validateArgs, ref i, "--file", logger, out var file))
                {
                    return CliExitCode.Failure;
                }

                validateContext.Files.Add(file);
                break;
            case "--strict":
                validateContext.Strict = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(validateArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                validateContext.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for validate: {validateArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (validateContext.Files.Count == 0)
    {
        logger.Error("At least one --file is required for validate.");
        return CliExitCode.Failure;
    }

    var validateWorker = new ValidateCommandWorker(
        validateContext,
        writeInfo: logger.Info,
        writeWarning: logger.Warning,
        writeError: logger.Error);

    return validateWorker.Process();
}

int RunDiff(string[] diffArgs)
{
    var diffContext = new DiffCommandContext();

    for (var i = 1; i < diffArgs.Length; i++)
    {
        switch (diffArgs[i])
        {
            case "--base":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--base", logger, out var basePath))
                {
                    return CliExitCode.Failure;
                }

                diffContext.BasePath = basePath;
                break;
            case "--head":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--head", logger, out var headPath))
                {
                    return CliExitCode.Failure;
                }

                diffContext.HeadPath = headPath;
                break;
            case "--fail-on-breaking":
                diffContext.FailOnBreaking = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                diffContext.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for diff: {diffArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (string.IsNullOrEmpty(diffContext.BasePath) || string.IsNullOrEmpty(diffContext.HeadPath))
    {
        logger.Error("Both --base and --head are required for diff.");
        return CliExitCode.Failure;
    }

    var diffWorker = new DiffCommandWorker(
        diffContext,
        writeInfo: logger.Info,
        writeWarning: logger.Warning,
        writeError: logger.Error);

    return diffWorker.Process();
}

static void PrintUsage()
{
    Console.WriteLine("Bielu.AspNetCore.AsyncApi CLI - Generate, merge, and validate AsyncAPI documents");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet asyncapi <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  getdocument    Generate AsyncAPI documents from ASP.NET Core applications");
    Console.WriteLine("  merge          Merge multiple AsyncAPI documents into one");
    Console.WriteLine("  validate       Validate AsyncAPI documents");
    Console.WriteLine("  diff           Compare two AsyncAPI documents for changes");
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
    Console.WriteLine("validate options:");
    Console.WriteLine("  --file <path>            Path or glob to AsyncAPI document(s) (required, repeatable)");
    Console.WriteLine("  --strict                 Treat warnings as errors");
    Console.WriteLine("  --format <text|json>     Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("diff options:");
    Console.WriteLine("  --base <path>            Path to the base (old) AsyncAPI document (required)");
    Console.WriteLine("  --head <path>            Path to the head (new) AsyncAPI document (required)");
    Console.WriteLine("  --fail-on-breaking       Exit with code 1 if breaking changes are detected");
    Console.WriteLine("  --format <text|json|markdown> Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("  -h, --help               Show this help message");
}
