// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Bielu.Overlay.Cli.Commands;

// Usage: dotnet overlay apply --file <path> --overlay <path> [--overlay <path> ...] [--output <path>] [--format json|yaml] [--strict]
// Usage: dotnet overlay validate --file <path> [--file <path> ...] [--strict] [--format text|json]

var logger = new ConsoleCliLogger();

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    PrintUsage();
    return CliExitCode.Success;
}

switch (args[0])
{
    case "apply":
        return RunApply(args);
    case "validate":
        return RunValidate(args);
    default:
        logger.Error($"Unknown command: {args[0]}");
        PrintUsage();
        return CliExitCode.Failure;
}

int RunApply(string[] applyArgs)
{
    var context = new ApplyCommandContext();

    for (var i = 1; i < applyArgs.Length; i++)
    {
        switch (applyArgs[i])
        {
            case "--file":
                if (!CliArgumentReader.TryReadValue(applyArgs, ref i, "--file", logger, out var file))
                {
                    return CliExitCode.Failure;
                }

                context.FilePath = file;
                break;
            case "--overlay":
                if (!CliArgumentReader.TryReadValue(applyArgs, ref i, "--overlay", logger, out var overlay))
                {
                    return CliExitCode.Failure;
                }

                context.Overlays.Add(overlay);
                break;
            case "--output":
                if (!CliArgumentReader.TryReadValue(applyArgs, ref i, "--output", logger, out var output))
                {
                    return CliExitCode.Failure;
                }

                context.OutputPath = output;
                break;
            case "--format":
                if (!CliArgumentReader.TryReadValue(applyArgs, ref i, "--format", logger, out var format))
                {
                    return CliExitCode.Failure;
                }

                context.Format = format;
                break;
            case "--strict":
                context.Strict = true;
                break;
            default:
                logger.Error($"Unknown argument for apply: {applyArgs[i]}");
                PrintUsage();
                return CliExitCode.Failure;
        }
    }

    if (string.IsNullOrEmpty(context.FilePath))
    {
        logger.Error("--file is required for apply.");
        return CliExitCode.Failure;
    }

    if (context.Overlays.Count == 0)
    {
        logger.Error("At least one --overlay is required for apply.");
        return CliExitCode.Failure;
    }

    var worker = new ApplyCommandWorker(context, logger.Info, logger.Warning, logger.Error);
    return worker.Process();
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

static void PrintUsage()
{
    Console.WriteLine("Bielu.Overlay.Cli - Apply and validate OpenAPI Overlays");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet overlay <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  apply          Apply overlays to an API description");
    Console.WriteLine("  validate       Validate overlay documents");
    Console.WriteLine();
    Console.WriteLine("apply options:");
    Console.WriteLine("  --file <path>            The description to transform - OpenAPI, AsyncAPI, or Arazzo (required)");
    Console.WriteLine("  --overlay <path>         An overlay to apply (required, repeatable - applied in order)");
    Console.WriteLine("  --output <path>          Where to write the result (default: standard output)");
    Console.WriteLine("  --format <json|yaml>     Output format (default: inferred from --output extension, else json)");
    Console.WriteLine("  --strict                 Treat a target matching zero nodes as an error");
    Console.WriteLine();
    Console.WriteLine("validate options:");
    Console.WriteLine("  --file <path>            Path or glob to overlay document(s) (required, repeatable)");
    Console.WriteLine("  --strict                 Treat warnings as errors");
    Console.WriteLine("  --format <text|json>     Output format (default: text)");
    Console.WriteLine();
    Console.WriteLine("  -h, --help               Show this help message");
}
