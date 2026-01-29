using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;

namespace StrykerAgent;

public static class Program
{
    public static int Main(string[] args)
    {
        var root = BuildRootCommand();
        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .UseParseErrorReporting(ExitCodes.InvalidArguments)
            .UseExceptionHandler(HandleException, ExitCodes.Unexpected)
            .Build();

        return parser.InvokeAsync(args).GetAwaiter().GetResult();
    }

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("AI-accessible interface for Stryker.NET mutation reports.");
        root.SetHandler(() => throw new AgentException(ExitCodes.InvalidArguments, "Missing command."));

        root.AddCommand(BuildSummaryCommand());
        root.AddCommand(BuildFilesCommand());
        root.AddCommand(BuildMutantsCommand());
        root.AddCommand(BuildMutantCommand());
        root.AddCommand(BuildExportCommand());

        return root;
    }

    private static Command BuildSummaryCommand()
    {
        var command = new Command("summary", "Summarize the mutation report.");
        var reportOption = CreateReportOption();
        var formatOption = new Option<string>("--format", () => "json", "Output format: json or text.");
        var prettyOption = CreatePrettyOption();

        command.AddOption(reportOption);
        command.AddOption(formatOption);
        command.AddOption(prettyOption);

        command.SetHandler(context =>
        {
            var reportPathOption = context.ParseResult.GetValueForOption(reportOption);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "json";
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = RunSummary(reportPathOption, format, pretty);
        });

        return command;
    }

    private static Command BuildFilesCommand()
    {
        var command = new Command("files", "List files with mutation summary information.");
        var reportOption = CreateReportOption();
        var formatOption = new Option<string>("--format", () => "json", "Output format: json or table.");
        var prettyOption = CreatePrettyOption();
        var sortOption = new Option<string>("--sort", () => "path", "Sort by: path, score, survived, mutants.");
        var limitOption = CreateNonNegativeIntOption("--limit", "Limit the number of files returned.");
        var statusOption = CreateMultiValueOption("--status", "Filter by status (one or more values).");

        command.AddOption(reportOption);
        command.AddOption(formatOption);
        command.AddOption(prettyOption);
        command.AddOption(sortOption);
        command.AddOption(limitOption);
        command.AddOption(statusOption);

        command.SetHandler(context =>
        {
            var reportPathOption = context.ParseResult.GetValueForOption(reportOption);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "json";
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var sort = context.ParseResult.GetValueForOption(sortOption) ?? "path";
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var statusValues = context.ParseResult.GetValueForOption(statusOption) ?? Array.Empty<string>();

            context.ExitCode = RunFiles(reportPathOption, format, pretty, sort, limit, statusValues);
        });

        return command;
    }

    private static Command BuildMutantsCommand()
    {
        var command = new Command("mutants", "List mutants from the report.");
        var reportOption = CreateReportOption();
        var formatOption = new Option<string>("--format", () => "json", "Output format: json or table.");
        var prettyOption = CreatePrettyOption();
        var statusOption = CreateMultiValueOption("--status", "Filter by status (one or more values).");
        var fileOption = CreateMultiValueOption("--file", "Filter by file path glob.");
        var mutatorOption = CreateMultiValueOption("--mutator", "Filter by mutator name.");
        var limitOption = CreateNonNegativeIntOption("--limit", "Limit the number of mutants returned.");
        var offsetOption = CreateNonNegativeIntOption("--offset", "Skip the first N mutants.");

        command.AddOption(reportOption);
        command.AddOption(formatOption);
        command.AddOption(prettyOption);
        command.AddOption(statusOption);
        command.AddOption(fileOption);
        command.AddOption(mutatorOption);
        command.AddOption(limitOption);
        command.AddOption(offsetOption);

        command.SetHandler(context =>
        {
            var reportPathOption = context.ParseResult.GetValueForOption(reportOption);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "json";
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var statusValues = context.ParseResult.GetValueForOption(statusOption) ?? Array.Empty<string>();
            var filePatterns = context.ParseResult.GetValueForOption(fileOption) ?? Array.Empty<string>();
            var mutatorValues = context.ParseResult.GetValueForOption(mutatorOption) ?? Array.Empty<string>();
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var offset = context.ParseResult.GetValueForOption(offsetOption) ?? 0;

            context.ExitCode = RunMutants(reportPathOption, format, pretty, statusValues, filePatterns, mutatorValues, limit, offset);
        });

        return command;
    }

    private static Command BuildMutantCommand()
    {
        var command = new Command("mutant", "Mutant commands.");
        command.SetHandler(() => throw new AgentException(ExitCodes.InvalidArguments, "Unknown mutant command."));
        var getCommand = new Command("get", "Get a single mutant by id.");
        var idArgument = new Argument<string>("id", "Mutant id.");
        var reportOption = CreateReportOption();
        var formatOption = new Option<string>("--format", () => "json", "Output format: json or text.");
        var prettyOption = CreatePrettyOption();

        getCommand.AddArgument(idArgument);
        getCommand.AddOption(reportOption);
        getCommand.AddOption(formatOption);
        getCommand.AddOption(prettyOption);

        getCommand.SetHandler(context =>
        {
            var mutantId = context.ParseResult.GetValueForArgument(idArgument);
            var reportPathOption = context.ParseResult.GetValueForOption(reportOption);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "json";
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = RunMutantGet(mutantId, reportPathOption, format, pretty);
        });

        command.AddCommand(getCommand);
        return command;
    }

    private static Command BuildExportCommand()
    {
        var command = new Command("export", "Export report data to jsonl or sqlite.");
        var reportOption = CreateReportOption();
        var outOption = new Option<string?>("--out", "Output directory.");
        var modeOption = new Option<string>("--mode", () => "jsonl", "Export mode: jsonl or sqlite.");
        var overwriteOption = new Option<bool>("--overwrite", "Overwrite existing output.");

        command.AddOption(reportOption);
        command.AddOption(outOption);
        command.AddOption(modeOption);
        command.AddOption(overwriteOption);

        command.SetHandler(context =>
        {
            var reportPathOption = context.ParseResult.GetValueForOption(reportOption);
            var outDir = context.ParseResult.GetValueForOption(outOption);
            var mode = context.ParseResult.GetValueForOption(modeOption) ?? "jsonl";
            var overwrite = context.ParseResult.GetValueForOption(overwriteOption);

            context.ExitCode = RunExport(reportPathOption, outDir, mode, overwrite);
        });

        return command;
    }

    private static Option<string?> CreateReportOption() =>
        new("--report", "Path to mutation-report.json.");

    private static Option<bool> CreatePrettyOption() =>
        new("--pretty", "Pretty-print JSON output.");

    private static Option<string[]> CreateMultiValueOption(string name, string description)
    {
        var option = new Option<string[]>(name, description)
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        return option;
    }

    private static Option<int?> CreateNonNegativeIntOption(string name, string description)
    {
        var option = new Option<int?>(name, description);
        option.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value.HasValue && value.Value < 0)
            {
                result.ErrorMessage = $"{name} must be a non-negative integer.";
            }
        });

        return option;
    }

    private static void HandleException(Exception exception, InvocationContext context)
    {
        if (exception is AgentException agentException)
        {
            Console.Error.WriteLine(agentException.Message);
            context.ExitCode = agentException.ExitCode;
            return;
        }

        Console.Error.WriteLine($"Unexpected error: {exception.Message}");
        context.ExitCode = ExitCodes.Unexpected;
    }

    private static int RunSummary(string? reportPathOption, string format, bool pretty)
    {
        EnsureFormat(format, ["json", "text"]);
        var reportPath = ResolveReportPath(reportPathOption);
        var data = ReportProcessor.LoadReportData(reportPath);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(data.Summary, pretty);
        }
        else
        {
            WriteSummaryText(data.Summary);
        }

        return ExitCodes.Success;
    }

    private static int RunFiles(
        string? reportPathOption,
        string format,
        bool pretty,
        string sort,
        int? limit,
        IReadOnlyList<string> statusValues)
    {
        EnsureFormat(format, ["json", "table"]);
        var statuses = NormalizeStatuses(statusValues);
        var reportPath = ResolveReportPath(reportPathOption);
        var data = ReportProcessor.LoadReportData(reportPath);

        IEnumerable<FileSummary> files = data.Files;
        if (statuses.Count > 0)
        {
            var statusSet = new HashSet<string>(statuses, StringComparer.Ordinal);
            files = files.Where(file => FileMatchesStatus(file, statusSet));
        }

        files = sort switch
        {
            "path" => files.OrderBy(file => file.FilePath, StringComparer.Ordinal),
            "score" => files
                .OrderByDescending(file => file.MutationScore)
                .ThenBy(file => file.FilePath, StringComparer.Ordinal),
            "survived" => files
                .OrderByDescending(file => file.StatusCounts.Survived)
                .ThenBy(file => file.FilePath, StringComparer.Ordinal),
            "mutants" => files
                .OrderByDescending(file => file.TotalMutants)
                .ThenBy(file => file.FilePath, StringComparer.Ordinal),
            _ => throw new AgentException(ExitCodes.InvalidArguments, $"Unknown sort option: {sort}")
        };

        if (limit.HasValue)
        {
            files = files.Take(limit.Value);
        }

        var fileList = files.ToList();
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(fileList, pretty);
        }
        else
        {
            TableFormatter.WriteFiles(fileList);
        }

        return ExitCodes.Success;
    }

    private static int RunMutants(
        string? reportPathOption,
        string format,
        bool pretty,
        IReadOnlyList<string> statusValues,
        IReadOnlyList<string> filePatterns,
        IReadOnlyList<string> mutatorValues,
        int? limit,
        int offset)
    {
        EnsureFormat(format, ["json", "table"]);
        if (offset < 0)
        {
            throw new AgentException(ExitCodes.InvalidArguments, "--offset must be a non-negative integer.");
        }

        var statuses = NormalizeStatuses(statusValues);
        var reportPath = ResolveReportPath(reportPathOption);
        var data = ReportProcessor.LoadReportData(reportPath);

        IEnumerable<MutantRecord> mutants = data.Mutants;
        if (statuses.Count > 0)
        {
            var statusSet = new HashSet<string>(statuses, StringComparer.Ordinal);
            mutants = mutants.Where(mutant => statusSet.Contains(mutant.Status));
        }

        if (filePatterns.Count > 0)
        {
            var matchers = filePatterns.Select(pattern => new GlobMatcher(pattern)).ToList();
            mutants = mutants.Where(mutant => matchers.Any(matcher => matcher.IsMatch(mutant.FilePath)));
        }

        if (mutatorValues.Count > 0)
        {
            var mutatorSet = new HashSet<string>(mutatorValues, StringComparer.Ordinal);
            mutants = mutants.Where(mutant => mutatorSet.Contains(mutant.Mutator));
        }

        mutants = mutants
            .OrderBy(mutant => mutant.FilePath, StringComparer.Ordinal)
            .ThenBy(mutant => mutant.Location.StartLine)
            .ThenBy(mutant => mutant.Id, StringComparer.Ordinal);

        if (offset > 0)
        {
            mutants = mutants.Skip(offset);
        }

        if (limit.HasValue)
        {
            mutants = mutants.Take(limit.Value);
        }

        var mutantList = mutants.ToList();
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(mutantList, pretty);
        }
        else
        {
            TableFormatter.WriteMutants(mutantList);
        }

        return ExitCodes.Success;
    }

    private static int RunMutantGet(string mutantId, string? reportPathOption, string format, bool pretty)
    {
        EnsureFormat(format, ["json", "text"]);
        var reportPath = ResolveReportPath(reportPathOption);
        var data = ReportProcessor.LoadReportData(reportPath);

        if (!data.MutantsById.TryGetValue(mutantId, out var mutant))
        {
            throw new AgentException(ExitCodes.EntityNotFound, $"Mutant not found: {mutantId}");
        }

        if (data.Report.Files == null || !data.Report.Files.TryGetValue(mutant.FilePath, out var file))
        {
            throw new AgentException(ExitCodes.ReportParseError, $"Report missing file for mutant: {mutant.FilePath}");
        }

        var context = new MutantContext
        {
            FilePath = mutant.FilePath,
            Language = file.Language ?? string.Empty,
            Source = file.Source ?? string.Empty,
            MutantLocation = mutant.Location
        };

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(new MutantGetResponse { Mutant = mutant, Context = context }, pretty);
        }
        else
        {
            WriteMutantText(mutant, context);
        }

        return ExitCodes.Success;
    }

    private static int RunExport(string? reportPathOption, string? outDir, string mode, bool overwrite)
    {
        if (!string.Equals(mode, "jsonl", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException(ExitCodes.InvalidArguments, $"Unknown export mode: {mode}");
        }

        var reportPath = ResolveReportPath(reportPathOption);
        var data = ReportProcessor.LoadReportData(reportPath);
        Exporter.Export(data, outDir ?? string.Empty, mode, overwrite);
        return ExitCodes.Success;
    }

    private static string ResolveReportPath(string? reportPathOption)
    {
        if (!string.IsNullOrWhiteSpace(reportPathOption))
        {
            if (File.Exists(reportPathOption))
            {
                return reportPathOption;
            }

            throw new AgentException(ExitCodes.ReportNotFound, $"Report not found: {reportPathOption}");
        }

        var directPath = Path.Combine(Environment.CurrentDirectory, "mutation-report.json");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var reportsPath = Path.Combine(Environment.CurrentDirectory, "reports", "mutation-report.json");
        if (File.Exists(reportsPath))
        {
            return reportsPath;
        }

        var strykerOutput = Path.Combine(Environment.CurrentDirectory, "StrykerOutput");
        if (Directory.Exists(strykerOutput))
        {
            var matches = Directory.EnumerateFiles(strykerOutput, "mutation-report.json", SearchOption.AllDirectories)
                .Where(IsReportInReportsFolder)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (matches.Count > 0)
            {
                return matches[0];
            }
        }

        throw new AgentException(ExitCodes.ReportNotFound, "Report not found.");
    }

    private static bool IsReportInReportsFolder(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.EndsWith("/reports/mutation-report.json", StringComparison.Ordinal);
    }

    private static List<string> NormalizeStatuses(IEnumerable<string> statusValues)
    {
        var normalized = new List<string>();
        foreach (var status in statusValues)
        {
            if (!StatusConstants.TryNormalize(status, out var value))
            {
                throw new AgentException(ExitCodes.InvalidArguments, $"Unknown status: {status}");
            }

            normalized.Add(value);
        }

        return normalized;
    }

    private static bool FileMatchesStatus(FileSummary file, HashSet<string> statuses)
    {
        foreach (var status in statuses)
        {
            var count = status switch
            {
                "Killed" => file.StatusCounts.Killed,
                "Survived" => file.StatusCounts.Survived,
                "NoCoverage" => file.StatusCounts.NoCoverage,
                "CompileError" => file.StatusCounts.CompileError,
                "RuntimeError" => file.StatusCounts.RuntimeError,
                "Timeout" => file.StatusCounts.Timeout,
                "Ignored" => file.StatusCounts.Ignored,
                "Pending" => file.StatusCounts.Pending,
                _ => 0
            };

            if (count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureFormat(string format, string[] allowed)
    {
        if (!allowed.Any(value => string.Equals(value, format, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentException(ExitCodes.InvalidArguments, $"Unknown format: {format}");
        }
    }

    private static void WriteJson<T>(T value, bool pretty)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty
        };

        var json = JsonSerializer.Serialize(value, options);
        Console.WriteLine(json);
    }

    private static void WriteSummaryText(RunSummary summary)
    {
        Console.WriteLine($"SchemaVersion: {summary.SchemaVersion}");
        Console.WriteLine($"Thresholds: high={summary.Thresholds.High} low={summary.Thresholds.Low}");
        Console.WriteLine($"Totals: files={summary.Totals.Files} mutants={summary.Totals.Mutants}");
        Console.WriteLine($"MutationScore: percent={summary.MutationScore.Percent.ToString("F2", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"MutationScore: killed={summary.MutationScore.NumeratorKilled} effective={summary.MutationScore.DenominatorEffective}");
        Console.WriteLine($"StatusCounts: Killed={summary.StatusCounts.Killed} Survived={summary.StatusCounts.Survived} NoCoverage={summary.StatusCounts.NoCoverage} CompileError={summary.StatusCounts.CompileError} RuntimeError={summary.StatusCounts.RuntimeError} Timeout={summary.StatusCounts.Timeout} Ignored={summary.StatusCounts.Ignored} Pending={summary.StatusCounts.Pending}");

        if (summary.PerformanceMs != null)
        {
            Console.WriteLine($"PerformanceMs: setup={summary.PerformanceMs.Setup.ToString(CultureInfo.InvariantCulture)} initialRun={summary.PerformanceMs.InitialRun.ToString(CultureInfo.InvariantCulture)} mutation={summary.PerformanceMs.Mutation.ToString(CultureInfo.InvariantCulture)}");
        }

        if (summary.Framework != null)
        {
            Console.WriteLine($"Framework: name={summary.Framework.Name} version={summary.Framework.Version ?? "null"}");
        }

        if (summary.System != null)
        {
            Console.WriteLine($"System: ci={summary.System.Ci.ToString().ToLowerInvariant()}");
        }
    }

    private static void WriteMutantText(MutantRecord mutant, MutantContext context)
    {
        Console.WriteLine($"Id: {mutant.Id}");
        Console.WriteLine($"Status: {mutant.Status}");
        Console.WriteLine($"File: {mutant.FilePath}");
        Console.WriteLine($"Language: {mutant.Language}");
        Console.WriteLine($"Mutator: {mutant.Mutator}");
        Console.WriteLine($"Location: {mutant.Location.StartLine}:{mutant.Location.StartColumn}-{mutant.Location.EndLine}:{mutant.Location.EndColumn}");
        Console.WriteLine($"Description: {mutant.Description ?? "null"}");
        Console.WriteLine($"Replacement: {mutant.Replacement ?? "null"}");
        Console.WriteLine($"StatusReason: {mutant.StatusReason ?? "null"}");
        Console.WriteLine($"DurationMs: {(mutant.DurationMs.HasValue ? mutant.DurationMs.Value.ToString(CultureInfo.InvariantCulture) : "null")}");
        Console.WriteLine($"TestsCompleted: {(mutant.TestsCompleted.HasValue ? mutant.TestsCompleted.Value.ToString(CultureInfo.InvariantCulture) : "null")}");
        Console.WriteLine($"IsStatic: {(mutant.IsStatic.HasValue ? mutant.IsStatic.Value.ToString().ToLowerInvariant() : "null")}");
        Console.WriteLine($"CoveredBy: {(mutant.CoveredBy.Count == 0 ? "[]" : string.Join(", ", mutant.CoveredBy.Select(test => test.Id)))}");
        Console.WriteLine($"KilledBy: {(mutant.KilledBy.Count == 0 ? "[]" : string.Join(", ", mutant.KilledBy.Select(test => test.Id)))}");
        Console.WriteLine($"ContextFile: {context.FilePath}");
        Console.WriteLine($"ContextLanguage: {context.Language}");
        Console.WriteLine("Source:");
        Console.WriteLine(context.Source);
    }
}
