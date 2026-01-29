namespace StrykerAgent;

public sealed class ReportData
{
    public MutationTestReport Report { get; }
    public IReadOnlyList<MutantRecord> Mutants { get; }
    public IReadOnlyDictionary<string, MutantRecord> MutantsById { get; }
    public IReadOnlyList<FileSummary> Files { get; }
    public RunSummary Summary { get; }
    public IReadOnlyDictionary<string, string> TestIndex { get; }

    public ReportData(
        MutationTestReport report,
        IReadOnlyList<MutantRecord> mutants,
        IReadOnlyDictionary<string, MutantRecord> mutantsById,
        IReadOnlyList<FileSummary> files,
        RunSummary summary,
        IReadOnlyDictionary<string, string> testIndex)
    {
        Report = report;
        Mutants = mutants;
        MutantsById = mutantsById;
        Files = files;
        Summary = summary;
        TestIndex = testIndex;
    }
}

public static class ReportProcessor
{
    public static ReportData LoadReportData(string reportPath)
    {
        var report = ReportLoader.Load(reportPath);
        return Normalize(report);
    }

    public static ReportData Normalize(MutationTestReport report)
    {
        var testIndex = BuildTestIndex(report.TestFiles);
        var mutants = BuildMutantRecords(report, testIndex);
        var mutantsById = new Dictionary<string, MutantRecord>(StringComparer.Ordinal);
        foreach (var mutant in mutants)
        {
            mutantsById.TryAdd(mutant.Id, mutant);
        }

        var files = BuildFileSummaries(report);
        var summary = BuildRunSummary(report, files);

        return new ReportData(report, mutants, mutantsById, files, summary, testIndex);
    }

    private static Dictionary<string, string> BuildTestIndex(Dictionary<string, ReportTestFile>? testFiles)
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        if (testFiles == null)
        {
            return index;
        }

        foreach (var key in testFiles.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!testFiles.TryGetValue(key, out var testFile) || testFile?.Tests == null)
            {
                continue;
            }

            foreach (var test in testFile.Tests)
            {
                if (test?.Id == null)
                {
                    continue;
                }

                index.TryAdd(test.Id, test.Name ?? string.Empty);
            }
        }

        return index;
    }

    private static List<MutantRecord> BuildMutantRecords(
        MutationTestReport report,
        IReadOnlyDictionary<string, string> testIndex)
    {
        var mutants = new List<MutantRecord>();
        if (report.Files == null)
        {
            return mutants;
        }

        foreach (var filePath in report.Files.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!report.Files.TryGetValue(filePath, out var file) || file?.Mutants == null)
            {
                continue;
            }

            foreach (var mutant in file.Mutants)
            {
                if (mutant?.Location?.Start == null || mutant.Location.End == null)
                {
                    continue;
                }

                var record = new MutantRecord
                {
                    Id = mutant.Id ?? string.Empty,
                    FilePath = filePath,
                    Language = file.Language ?? string.Empty,
                    Location = new MutantLocation
                    {
                        StartLine = mutant.Location.Start.Line ?? 0,
                        StartColumn = mutant.Location.Start.Column ?? 0,
                        EndLine = mutant.Location.End.Line ?? 0,
                        EndColumn = mutant.Location.End.Column ?? 0
                    },
                    Mutator = mutant.MutatorName ?? string.Empty,
                    Status = mutant.Status ?? string.Empty,
                    Description = mutant.Description,
                    Replacement = mutant.Replacement,
                    StatusReason = mutant.StatusReason,
                    DurationMs = mutant.Duration,
                    TestsCompleted = mutant.TestsCompleted,
                    CoveredBy = BuildTestRefs(mutant.CoveredBy, testIndex),
                    KilledBy = BuildTestRefs(mutant.KilledBy, testIndex),
                    IsStatic = mutant.Static
                };

                mutants.Add(record);
            }
        }

        return mutants;
    }

    private static List<TestRef> BuildTestRefs(List<string>? ids, IReadOnlyDictionary<string, string> testIndex)
    {
        if (ids == null || ids.Count == 0)
        {
            return [];
        }

        var list = new List<TestRef>(ids.Count);
        foreach (var id in ids)
        {
            testIndex.TryGetValue(id, out var name);
            list.Add(new TestRef { Id = id, Name = string.IsNullOrEmpty(name) ? null : name });
        }

        return list;
    }

    private static List<FileSummary> BuildFileSummaries(MutationTestReport report)
    {
        var files = new List<FileSummary>();
        if (report.Files == null)
        {
            return files;
        }

        foreach (var filePath in report.Files.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!report.Files.TryGetValue(filePath, out var file) || file?.Mutants == null)
            {
                continue;
            }

            var counts = new StatusCounts();
            foreach (var mutant in file.Mutants)
            {
                if (mutant?.Status == null)
                {
                    continue;
                }

                counts.Increment(mutant.Status);
            }

            var totalMutants = file.Mutants.Count;
            var mutationScore = ComputeMutationScore(counts);

            files.Add(new FileSummary
            {
                FilePath = filePath,
                Language = file.Language ?? string.Empty,
                StatusCounts = counts,
                TotalMutants = totalMutants,
                MutationScore = mutationScore.Percent
            });
        }

        return files;
    }

    private static RunSummary BuildRunSummary(MutationTestReport report, IReadOnlyList<FileSummary> files)
    {
        var counts = new StatusCounts();
        foreach (var file in files)
        {
            counts.Add(file.StatusCounts);
        }

        var mutationScore = ComputeMutationScore(counts);
        var totalMutants = files.Sum(file => file.TotalMutants);

        var summary = new RunSummary
        {
            SchemaVersion = report.SchemaVersion ?? string.Empty,
            Thresholds = new RunThresholds
            {
                High = report.Thresholds?.High ?? 0,
                Low = report.Thresholds?.Low ?? 0
            },
            Totals = new RunTotals
            {
                Files = files.Count,
                Mutants = totalMutants
            },
            StatusCounts = counts,
            MutationScore = mutationScore
        };

        if (report.Performance != null)
        {
            summary.PerformanceMs = new PerformanceMs
            {
                Setup = report.Performance.Setup ?? 0,
                InitialRun = report.Performance.InitialRun ?? 0,
                Mutation = report.Performance.Mutation ?? 0
            };
        }

        if (report.Framework != null)
        {
            summary.Framework = new FrameworkInfo
            {
                Name = report.Framework.Name ?? string.Empty,
                Version = report.Framework.Version
            };
        }

        if (report.System?.Ci != null)
        {
            summary.System = new SystemInfo
            {
                Ci = report.System.Ci.Value
            };
        }

        return summary;
    }

    private static MutationScore ComputeMutationScore(StatusCounts counts)
    {
        var denominator =
            counts.Killed +
            counts.Survived +
            counts.Timeout +
            counts.NoCoverage +
            counts.CompileError +
            counts.RuntimeError;

        var numerator = counts.Killed;
        var percent = denominator == 0 ? 0.0 : numerator / (double)denominator * 100.0;

        return new MutationScore
        {
            Percent = percent,
            NumeratorKilled = numerator,
            DenominatorEffective = denominator
        };
    }
}
