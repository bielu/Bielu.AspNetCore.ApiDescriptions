// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Bielu.Cli.Shared;

// Usage: dotnet arazzo validate --file <path> [--file <path> ...] [--source <name>=<path> ...] [--resolve-local-sources] [--strict] [--format text|json]
// Usage: dotnet arazzo lint --file <path> [--file <path> ...] [--strict] [--format text|json]
// Usage: dotnet arazzo diff --base <path> --head <path> [--fail-on-breaking] [--format text|json|markdown]
// Usage: dotnet arazzo impact --file <path> --base <name>=<path> --head <name>=<path> [...] [--fail-on-breaking] [--format text|json|markdown]

var logger = new ConsoleCliLogger();

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    PrintUsage();
    return CliExitCode.Success;
}

switch (args[0])
{
    case "validate":
        return RunValidate(args);
    case "lint":
        return RunLint(args);
    case "diff":
        return RunDiff(args);
    case "impact":
        return RunImpact(args);
    default:
        logger.Error($"Unknown command: {args[0]}");
        PrintUsage();
        return CliExitCode.Failure;
}

int RunValidate(string[] validateArgs)
{
    var context = new ValidateCommandContext();

    for (var i = 1; i < validateArgs.Length; i++)
    {
        switch (validateArgs[i])
        {
            case "--file":
                if (!CliArgumentReader.TryReadValue(validateArgs, ref i, "--file", logger, out var file))
                {
                    return CliExitCode.Failure;
                }

                context.Files.Add(file);
                break;
            case "--source":
                if (!TryReadSource(validateArgs, ref i, "--source", context.Sources))
                {
                    return CliExitCode.Failure;
                }

                break;
            case "--resolve-local-sources":
                context.ResolveLocalSources = true;
                break;
            case "--strict":
                context.Strict = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(validateArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                context.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for validate: {validateArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (context.Files.Count == 0)
    {
        logger.Error("At least one --file is required for validate.");
        return CliExitCode.Failure;
    }

    var worker = new ValidateCommandWorker(context, logger.Info, logger.Warning, logger.Error);
    return worker.Process();
}

int RunLint(string[] lintArgs)
{
    var context = new LintCommandContext();

    for (var i = 1; i < lintArgs.Length; i++)
    {
        switch (lintArgs[i])
        {
            case "--file":
                if (!CliArgumentReader.TryReadValue(lintArgs, ref i, "--file", logger, out var file))
                {
                    return CliExitCode.Failure;
                }

                context.Files.Add(file);
                break;
            case "--strict":
                context.Strict = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(lintArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                context.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for lint: {lintArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (context.Files.Count == 0)
    {
        logger.Error("At least one --file is required for lint.");
        return CliExitCode.Failure;
    }

    var worker = new LintCommandWorker(context, logger.Info, logger.Warning, logger.Error);
    return worker.Process();
}

int RunDiff(string[] diffArgs)
{
    var context = new DiffCommandContext();

    for (var i = 1; i < diffArgs.Length; i++)
    {
        switch (diffArgs[i])
        {
            case "--base":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--base", logger, out var basePath))
                {
                    return CliExitCode.Failure;
                }

                context.BasePath = basePath;
                break;
            case "--head":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--head", logger, out var headPath))
                {
                    return CliExitCode.Failure;
                }

                context.HeadPath = headPath;
                break;
            case "--fail-on-breaking":
                context.FailOnBreaking = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(diffArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                context.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for diff: {diffArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (string.IsNullOrEmpty(context.BasePath) || string.IsNullOrEmpty(context.HeadPath))
    {
        logger.Error("Both --base and --head are required for diff.");
        return CliExitCode.Failure;
    }

    var worker = new DiffCommandWorker(context, logger.Info, logger.Warning, logger.Error);
    return worker.Process();
}

int RunImpact(string[] impactArgs)
{
    var context = new ImpactCommandContext();

    for (var i = 1; i < impactArgs.Length; i++)
    {
        switch (impactArgs[i])
        {
            case "--file":
                if (!CliArgumentReader.TryReadValue(impactArgs, ref i, "--file", logger, out var file))
                {
                    return CliExitCode.Failure;
                }

                context.File = file;
                break;
            case "--base":
                if (!TryReadSource(impactArgs, ref i, "--base", context.BaseSources))
                {
                    return CliExitCode.Failure;
                }

                break;
            case "--head":
                if (!TryReadSource(impactArgs, ref i, "--head", context.HeadSources))
                {
                    return CliExitCode.Failure;
                }

                break;
            case "--fail-on-breaking":
                context.FailOnBreaking = true;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(impactArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                context.Format = format;
                break;
            default:
                logger.Error($"Unknown argument for impact: {impactArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (string.IsNullOrEmpty(context.File) || context.BaseSources.Count == 0 || context.HeadSources.Count == 0)
    {
        logger.Error("impact requires --file and at least one --base and one --head.");
        return CliExitCode.Failure;
    }

    var worker = new ImpactCommandWorker(context, logger.Info, logger.Warning, logger.Error);
    return worker.Process();
}

// Reads a <name>=<path> value (--source, --base, --head) into sources.
bool TryReadSource(string[] commandArgs, ref int i, string option, Dictionary<string, string> sources)
{
    if (!CliArgumentReader.TryReadValue(commandArgs, ref i, option, logger, out var value))
    {
        return false;
    }

    var separator = value.IndexOf('=');
    if (separator <= 0 || separator == value.Length - 1)
    {
        logger.Error($"{option} expects <name>=<path>, but got '{value}'.");
        return false;
    }

    sources[value[..separator]] = value[(separator + 1)..];
    return true;
}

static void PrintUsage()
{
    Console.WriteLine("Bielu.Arazzo.Cli - Validate, lint, and diff Arazzo workflow documents");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet arazzo <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  validate       Validate Arazzo documents (structural invariants)");
    Console.WriteLine("  lint           Lint Arazzo documents (style and graph-shape checks)");
    Console.WriteLine("  diff           Compare two Arazzo documents for changes");
    Console.WriteLine("  impact         List the workflow steps an OpenAPI/AsyncAPI change affects");
    Console.WriteLine();
    Console.WriteLine("validate/lint options:");
    Console.WriteLine("  --file <path>            Path or glob to Arazzo document(s) (required, repeatable)");
    Console.WriteLine("  --strict                 Treat warnings as errors");
    Console.WriteLine("  --format <text|json>     Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("validate only:");
    Console.WriteLine("  --source <name>=<path>   Check step references against the OpenAPI/AsyncAPI file for the");
    Console.WriteLine("                           source description <name> (repeatable)");
    Console.WriteLine("  --resolve-local-sources  Also load each source whose url is a relative path to a file");
    Console.WriteLine();
    Console.WriteLine("diff options:");
    Console.WriteLine("  --base <path>            Path to the base (old) Arazzo document (required)");
    Console.WriteLine("  --head <path>            Path to the head (new) Arazzo document (required)");
    Console.WriteLine("  --fail-on-breaking       Exit with code 1 if breaking changes are detected");
    Console.WriteLine("  --format <text|json|markdown> Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("impact options:");
    Console.WriteLine("  --file <path>            Path to the Arazzo document (required)");
    Console.WriteLine("  --base <name>=<path>     The source description <name>'s document before the change (repeatable)");
    Console.WriteLine("  --head <name>=<path>     The source description <name>'s document after the change (repeatable)");
    Console.WriteLine("  --fail-on-breaking       Exit with code 1 if a step's target no longer resolves");
    Console.WriteLine("  --format <text|json|markdown> Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("  -h, --help               Show this help message");
}
