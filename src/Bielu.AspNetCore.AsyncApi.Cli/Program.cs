// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.AspNetCore.AsyncApi.Cli.Hosting;
using Bielu.Cli.Shared;

// Parse command line arguments
// Usage: dotnet asyncapi getdocument --assembly <name> --assembly-path <path> --output <dir> --project <name> [--document <name>] [--file-list <path>] [--file-name <name>]
// Usage: dotnet asyncapi merge --source <uri> [--source <uri> ...] --output <path> [--prefix <prefix> ...] [--title <title>] [--version <version>]
// Usage: dotnet asyncapi likec4 --source [name=]<path|url> [--source ...] [--arazzo [name=]<path|url> ...] [--link <name>=<url> ...] --output <path>
// Usage: dotnet asyncapi likec4 check --model <likec4.json> --source [name=]<path|url> [--source ...] [--strict] [--format <text|json>]

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

if (args[0] == "likec4")
{
    return RunLikeC4(args);
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
            if (!CliArgumentReader.TryReadValue(args, ref i, "--assembly", logger, out var assemblyName))
            {
                return CliExitCode.Failure;
            }

            context.AssemblyName = assemblyName;
            break;
        case "--assembly-path":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--assembly-path", logger, out var assemblyPath))
            {
                return CliExitCode.Failure;
            }

            context.AssemblyPath = assemblyPath;
            break;
        case "--output":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--output", logger, out var outputDirectory))
            {
                return CliExitCode.Failure;
            }

            context.OutputDirectory = outputDirectory;
            break;
        case "--project":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--project", logger, out var projectName))
            {
                return CliExitCode.Failure;
            }

            context.ProjectName = projectName;
            break;
        case "--document":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--document", logger, out var documentName))
            {
                return CliExitCode.Failure;
            }

            context.DocumentName = documentName;
            break;
        case "--file-list":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--file-list", logger, out var fileListPath))
            {
                return CliExitCode.Failure;
            }

            context.FileListPath = fileListPath;
            break;
        case "--file-name":
            if (!CliArgumentReader.TryReadValue(args, ref i, "--file-name", logger, out var fileName))
            {
                return CliExitCode.Failure;
            }

            context.FileName = fileName;
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

// An application built for another runtime is generated on that runtime, in a relaunched worker.
if (TargetRuntimeLauncher.TryRelaunch(context, args, logger, out var relaunchExitCode))
{
    return relaunchExitCode;
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

int RunLikeC4(string[] likeC4Args)
{
    if (likeC4Args.Length > 1 && likeC4Args[1] == "check")
    {
        return RunLikeC4Check(likeC4Args);
    }

    var likeC4Context = new LikeC4CommandContext();

    for (var i = 1; i < likeC4Args.Length; i++)
    {
        switch (likeC4Args[i])
        {
            case "--source":
                if (!CliArgumentReader.TryReadValue(likeC4Args, ref i, "--source", logger, out var source))
                {
                    return CliExitCode.Failure;
                }

                likeC4Context.Sources.Add(source);
                break;
            case "--arazzo":
                if (!CliArgumentReader.TryReadValue(likeC4Args, ref i, "--arazzo", logger, out var workflow))
                {
                    return CliExitCode.Failure;
                }

                likeC4Context.Workflows.Add(workflow);
                break;
            case "--link":
                if (!CliArgumentReader.TryReadValue(likeC4Args, ref i, "--link", logger, out var link))
                {
                    return CliExitCode.Failure;
                }

                likeC4Context.Links.Add(link);
                break;
            case "--output":
                if (!CliArgumentReader.TryReadValue(likeC4Args, ref i, "--output", logger, out var output))
                {
                    return CliExitCode.Failure;
                }

                likeC4Context.OutputPath = output;
                break;
            default:
                logger.Error($"Unknown argument for likec4: {likeC4Args[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (likeC4Context.Sources.Count == 0 && likeC4Context.Workflows.Count == 0)
    {
        logger.Error("At least one --source or --arazzo is required for likec4.");
        return CliExitCode.Failure;
    }

    if (string.IsNullOrEmpty(likeC4Context.OutputPath))
    {
        logger.Error("--output is required for likec4.");
        return CliExitCode.Failure;
    }

    var likeC4Worker = new LikeC4CommandWorker(
        likeC4Context,
        writeInfo: logger.Info,
        writeError: logger.Error);

    return likeC4Worker.Process();
}

int RunLikeC4Check(string[] checkArgs)
{
    var checkContext = new LikeC4CheckCommandContext();

    for (var i = 2; i < checkArgs.Length; i++)
    {
        switch (checkArgs[i])
        {
            case "--model":
                if (!CliArgumentReader.TryReadValue(checkArgs, ref i, "--model", logger, out var model))
                {
                    return CliExitCode.Failure;
                }

                checkContext.ModelPath = model;
                break;
            case "--source":
                if (!CliArgumentReader.TryReadValue(checkArgs, ref i, "--source", logger, out var source))
                {
                    return CliExitCode.Failure;
                }

                checkContext.Sources.Add(source);
                break;
            case "--strict":
                checkContext.Strict = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(checkArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                checkContext.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for likec4 check: {checkArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (string.IsNullOrEmpty(checkContext.ModelPath))
    {
        logger.Error("--model is required for likec4 check.");
        return CliExitCode.Failure;
    }

    if (checkContext.Sources.Count == 0)
    {
        logger.Error("At least one --source is required for likec4 check.");
        return CliExitCode.Failure;
    }

    var checkWorker = new LikeC4CheckCommandWorker(
        checkContext,
        writeInfo: logger.Info,
        writeWarning: logger.Warning,
        writeError: logger.Error);

    return checkWorker.Process();
}

static void PrintUsage()
{
    Console.WriteLine("Bielu.AspNetCore.AsyncApi CLI - Generate, merge, validate, and diagram AsyncAPI documents");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet asyncapi <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  getdocument    Generate AsyncAPI documents from ASP.NET Core applications");
    Console.WriteLine("  merge          Merge multiple AsyncAPI documents into one");
    Console.WriteLine("  validate       Validate AsyncAPI documents");
    Console.WriteLine("  diff           Compare two AsyncAPI documents for changes");
    Console.WriteLine("  likec4         Generate a LikeC4 architecture model from AsyncAPI documents");
    Console.WriteLine("  likec4 check   Check a LikeC4 model against the AsyncAPI documents it describes");
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
    Console.WriteLine("likec4 options:");
    Console.WriteLine("  --source [name=]<path|url> An AsyncAPI document, one per service (repeatable; an unnamed path may be a glob)");
    Console.WriteLine("  --arazzo [name=]<path|url> An Arazzo document whose workflows become dynamic views (repeatable)");
    Console.WriteLine("  --link <name>=<url>      Where readers find a document (default: its URL, or its path from the model)");
    Console.WriteLine("  --output <path>          The .c4 file to write (required)");
    Console.WriteLine();
    Console.WriteLine("likec4 check options (check a LikeC4 model against the AsyncAPI documents it describes):");
    Console.WriteLine("  --model <path|url>       The model, as 'likec4 export json' writes it (required)");
    Console.WriteLine("  --source [name=]<path|url> An AsyncAPI document, named as the model's asyncapi metadata names it (required, repeatable)");
    Console.WriteLine("  --strict                 Treat warnings (documents no element stands for) as errors");
    Console.WriteLine("  --format <text|json>     Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("  -h, --help               Show this help message");
}
