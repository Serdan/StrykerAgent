using System.Globalization;
using System.Text.Json;

namespace StrykerAgent;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (AgentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return ExitCodes.Unexpected;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            throw new AgentException(ExitCodes.InvalidArguments, "Missing command.");
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        return command switch
        {
            "summary" => RunSummary(rest),
            "files" => RunFiles(rest),
            "mutants" => RunMutants(rest),
            "mutant" => RunMutant(rest),
            "export" => RunExport(rest),
            _ => throw new AgentException(ExitCodes.InvalidArguments, $"Unknown command: {command}")
        };
    }

    private static int RunSummary(string[] args)
    {
        string? reportPathOption = null;
        var format = "json";
        var pretty = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report":
                    reportPathOption = RequireValue(args, ref i, "--report");
                    break;
                case "--format":
                    format = RequireValue(args, ref i, "--format");
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                default:
                    throw new AgentException(ExitCodes.InvalidArguments, $"Unknown option: {args[i]}");
            }
        }

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

    private static int RunFiles(string[] args)
    {
        string? reportPathOption = null;
        var format = "json";
        var pretty = false;
        var sort = "path";
        int? limit = null;
        var statusValues = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report":
                    reportPathOption = RequireValue(args, ref i, "--report");
                    break;
                case "--format":
                    format = RequireValue(args, ref i, "--format");
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                case "--sort":
                    sort = RequireValue(args, ref i, "--sort");
                    break;
                case "--limit":
                    limit = ParseNonNegativeInt(RequireValue(args, ref i, "--limit"), "--limit");
                    break;
                case "--status":
                    statusValues.AddRange(ReadValues(args, ref i, "--status"));
                    break;
                default:
                    throw new AgentException(ExitCodes.InvalidArguments, $"Unknown option: {args[i]}");
            }
        }

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

    private static int RunMutants(string[] args)
    {
        string? reportPathOption = null;
        var format = "json";
        var pretty = false;
        var statusValues = new List<string>();
        var filePatterns = new List<string>();
        var mutatorValues = new List<string>();
        int? limit = null;
        var offset = 0;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report":
                    reportPathOption = RequireValue(args, ref i, "--report");
                    break;
                case "--format":
                    format = RequireValue(args, ref i, "--format");
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                case "--status":
                    statusValues.AddRange(ReadValues(args, ref i, "--status"));
                    break;
                case "--file":
                    filePatterns.AddRange(ReadValues(args, ref i, "--file"));
                    break;
                case "--mutator":
                    mutatorValues.AddRange(ReadValues(args, ref i, "--mutator"));
                    break;
                case "--limit":
                    limit = ParseNonNegativeInt(RequireValue(args, ref i, "--limit"), "--limit");
                    break;
                case "--offset":
                    offset = ParseNonNegativeInt(RequireValue(args, ref i, "--offset"), "--offset");
                    break;
                default:
                    throw new AgentException(ExitCodes.InvalidArguments, $"Unknown option: {args[i]}");
            }
        }

        EnsureFormat(format, ["json", "table"]);
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

    private static int RunMutant(string[] args)
    {
        if (args.Length == 0 || args[0] != "get")
        {
            throw new AgentException(ExitCodes.InvalidArguments, "Unknown mutant command.");
        }

        if (args.Length < 2)
        {
            throw new AgentException(ExitCodes.InvalidArguments, "mutant get requires <id>.");
        }

        var mutantId = args[1];
        var format = "json";
        var pretty = false;
        string? reportPathOption = null;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report":
                    reportPathOption = RequireValue(args, ref i, "--report");
                    break;
                case "--format":
                    format = RequireValue(args, ref i, "--format");
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                default:
                    throw new AgentException(ExitCodes.InvalidArguments, $"Unknown option: {args[i]}");
            }
        }

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

    private static int RunExport(string[] args)
    {
        string? reportPathOption = null;
        string? outDir = null;
        var mode = "jsonl";
        var overwrite = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report":
                    reportPathOption = RequireValue(args, ref i, "--report");
                    break;
                case "--out":
                    outDir = RequireValue(args, ref i, "--out");
                    break;
                case "--mode":
                    mode = RequireValue(args, ref i, "--mode");
                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                default:
                    throw new AgentException(ExitCodes.InvalidArguments, $"Unknown option: {args[i]}");
            }
        }

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

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new AgentException(ExitCodes.InvalidArguments, $"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }

    private static List<string> ReadValues(string[] args, ref int index, string optionName)
    {
        var values = new List<string>();
        while (index + 1 < args.Length && !IsOption(args[index + 1]))
        {
            index++;
            values.Add(args[index]);
        }

        if (values.Count == 0)
        {
            throw new AgentException(ExitCodes.InvalidArguments, $"Missing value for {optionName}.");
        }

        return values;
    }

    private static bool IsOption(string value) =>
        value.StartsWith("--", StringComparison.Ordinal);

    private static int ParseNonNegativeInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new AgentException(ExitCodes.InvalidArguments, $"{optionName} must be a non-negative integer.");
        }

        return parsed;
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
