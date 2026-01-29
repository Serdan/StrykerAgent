using System.Text.Json;

namespace StrykerAgent;

public static class ReportLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static MutationTestReport Load(string path)
    {
        MutationTestReport? report;
        try
        {
            var json = File.ReadAllText(path);
            report = JsonSerializer.Deserialize<MutationTestReport>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AgentException(ExitCodes.ReportParseError, $"Report parse error: {ex.Message}");
        }

        if (report == null)
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report parse error: empty document.");
        }

        ValidateReport(report);
        return report;
    }

    private static void ValidateReport(MutationTestReport report)
    {
        if (string.IsNullOrWhiteSpace(report.SchemaVersion))
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report missing required field: schemaVersion.");
        }

        if (report.Thresholds == null)
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report missing required field: thresholds.");
        }

        if (report.Thresholds.High is null || report.Thresholds.Low is null)
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report thresholds must include high and low.");
        }

        if (report.Thresholds.High < 0 || report.Thresholds.High > 100 ||
            report.Thresholds.Low < 0 || report.Thresholds.Low > 100)
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report thresholds must be between 0 and 100.");
        }

        if (report.Files == null)
        {
            throw new AgentException(ExitCodes.ReportParseError, "Report missing required field: files.");
        }

        foreach (var (filePath, file) in report.Files)
        {
            if (file == null)
            {
                throw new AgentException(ExitCodes.ReportParseError, $"Report file entry is null: {filePath}.");
            }

            if (string.IsNullOrWhiteSpace(file.Language))
            {
                throw new AgentException(ExitCodes.ReportParseError, $"Report file missing language: {filePath}.");
            }

            if (file.Mutants == null)
            {
                throw new AgentException(ExitCodes.ReportParseError, $"Report file missing mutants: {filePath}.");
            }

            foreach (var mutant in file.Mutants)
            {
                if (mutant == null)
                {
                    throw new AgentException(ExitCodes.ReportParseError, $"Null mutant entry in file: {filePath}.");
                }

                if (string.IsNullOrWhiteSpace(mutant.Id))
                {
                    throw new AgentException(ExitCodes.ReportParseError, $"Mutant missing id in file: {filePath}.");
                }

                if (string.IsNullOrWhiteSpace(mutant.MutatorName))
                {
                    throw new AgentException(ExitCodes.ReportParseError, $"Mutant missing mutatorName: {mutant.Id}.");
                }

                if (string.IsNullOrWhiteSpace(mutant.Status) || !StatusConstants.IsValid(mutant.Status))
                {
                    throw new AgentException(ExitCodes.ReportParseError, $"Mutant has invalid status: {mutant.Id}.");
                }

                ValidateLocation(mutant.Location, mutant.Id);

                ValidateStringList(mutant.CoveredBy, $"coveredBy for mutant {mutant.Id}");
                ValidateStringList(mutant.KilledBy, $"killedBy for mutant {mutant.Id}");
            }
        }

        if (report.TestFiles != null)
        {
            foreach (var (testFileKey, testFile) in report.TestFiles)
            {
                if (testFile?.Tests == null)
                {
                    throw new AgentException(ExitCodes.ReportParseError, $"Test file missing tests: {testFileKey}.");
                }

                foreach (var test in testFile.Tests)
                {
                    if (test == null)
                    {
                        throw new AgentException(ExitCodes.ReportParseError, $"Null test entry in test file: {testFileKey}.");
                    }

                    if (string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.Name))
                    {
                        throw new AgentException(ExitCodes.ReportParseError, $"Test entry missing id or name in: {testFileKey}.");
                    }
                }
            }
        }

        if (report.Performance != null)
        {
            if (report.Performance.Setup is null ||
                report.Performance.InitialRun is null ||
                report.Performance.Mutation is null)
            {
                throw new AgentException(ExitCodes.ReportParseError, "Performance section must include setup, initialRun, and mutation.");
            }
        }

        if (report.Framework != null)
        {
            if (string.IsNullOrWhiteSpace(report.Framework.Name))
            {
                throw new AgentException(ExitCodes.ReportParseError, "Framework section must include name.");
            }

            if (report.Framework.Branding != null &&
                string.IsNullOrWhiteSpace(report.Framework.Branding.HomepageUrl))
            {
                throw new AgentException(ExitCodes.ReportParseError, "Framework branding must include homepageUrl.");
            }
        }

        if (report.System != null)
        {
            if (report.System.Ci is null)
            {
                throw new AgentException(ExitCodes.ReportParseError, "System section must include ci.");
            }

            if (report.System.Os != null && string.IsNullOrWhiteSpace(report.System.Os.Platform))
            {
                throw new AgentException(ExitCodes.ReportParseError, "System os must include platform.");
            }

            if (report.System.Cpu != null && report.System.Cpu.LogicalCores is null)
            {
                throw new AgentException(ExitCodes.ReportParseError, "System cpu must include logicalCores.");
            }

            if (report.System.Ram != null && report.System.Ram.Total is null)
            {
                throw new AgentException(ExitCodes.ReportParseError, "System ram must include total.");
            }
        }
    }

    private static void ValidateLocation(ReportLocation? location, string mutantId)
    {
        if (location?.Start == null || location.End == null)
        {
            throw new AgentException(ExitCodes.ReportParseError, $"Mutant missing location: {mutantId}.");
        }

        if (location.Start.Line is null || location.Start.Column is null ||
            location.End.Line is null || location.End.Column is null)
        {
            throw new AgentException(ExitCodes.ReportParseError, $"Mutant location missing line/column: {mutantId}.");
        }

        if (location.Start.Line < 1 || location.Start.Column < 1 ||
            location.End.Line < 1 || location.End.Column < 1)
        {
            throw new AgentException(ExitCodes.ReportParseError, $"Mutant location must be 1-based: {mutantId}.");
        }
    }

    private static void ValidateStringList(List<string>? values, string label)
    {
        if (values == null)
        {
            return;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AgentException(ExitCodes.ReportParseError, $"Invalid {label} entry.");
            }
        }
    }
}
