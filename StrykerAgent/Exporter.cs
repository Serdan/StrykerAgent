using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StrykerAgent;

public static class Exporter
{
    public static void Export(ReportData data, string outDir, string mode, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(outDir))
        {
            throw new AgentException(ExitCodes.InvalidArguments, "Export requires --out <dir>.");
        }

        if (Directory.Exists(outDir) || File.Exists(outDir))
        {
            if (!overwrite)
            {
                throw new AgentException(ExitCodes.ExportExists, $"Export destination exists: {outDir}");
            }

            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, true);
            }
            else if (File.Exists(outDir))
            {
                File.Delete(outDir);
            }
        }

        Directory.CreateDirectory(outDir);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        var summaryPath = Path.Combine(outDir, "summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(data.Summary, jsonOptions), new UTF8Encoding(false));

        WriteJsonl(Path.Combine(outDir, "files.jsonl"), data.Files, jsonOptions);
        WriteJsonl(Path.Combine(outDir, "mutants.jsonl"), data.Mutants, jsonOptions);

        if (string.Equals(mode, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            WriteSqlite(Path.Combine(outDir, "stryker-report.sqlite"), data);
        }
    }

    private static void WriteJsonl<T>(string path, IEnumerable<T> records, JsonSerializerOptions jsonOptions)
    {
        using var stream = File.Create(path);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        foreach (var record in records)
        {
            var line = JsonSerializer.Serialize(record, jsonOptions);
            writer.WriteLine(line);
        }
    }

    private static void WriteSqlite(string path, ReportData data)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        CreateTables(connection);

        using var transaction = connection.BeginTransaction();
        InsertSummary(connection, transaction, data.Summary);
        InsertFiles(connection, transaction, data.Files);
        InsertMutants(connection, transaction, data.Mutants);
        InsertMutantTests(connection, transaction, "mutant_covered_by", data.Mutants, mutant => mutant.CoveredBy);
        InsertMutantTests(connection, transaction, "mutant_killed_by", data.Mutants, mutant => mutant.KilledBy);
        transaction.Commit();
    }

    private static void CreateTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE summary (
                schemaVersion TEXT NOT NULL,
                thresholdsHigh INTEGER NOT NULL,
                thresholdsLow INTEGER NOT NULL,
                totalFiles INTEGER NOT NULL,
                totalMutants INTEGER NOT NULL,
                statusKilled INTEGER NOT NULL,
                statusSurvived INTEGER NOT NULL,
                statusNoCoverage INTEGER NOT NULL,
                statusCompileError INTEGER NOT NULL,
                statusRuntimeError INTEGER NOT NULL,
                statusTimeout INTEGER NOT NULL,
                statusIgnored INTEGER NOT NULL,
                statusPending INTEGER NOT NULL,
                mutationScorePercent REAL NOT NULL,
                mutationScoreNumeratorKilled INTEGER NOT NULL,
                mutationScoreDenominatorEffective INTEGER NOT NULL,
                performanceSetup REAL NULL,
                performanceInitialRun REAL NULL,
                performanceMutation REAL NULL,
                frameworkName TEXT NULL,
                frameworkVersion TEXT NULL,
                systemCi INTEGER NULL
            );

            CREATE TABLE files (
                filePath TEXT PRIMARY KEY,
                language TEXT NOT NULL,
                mutationScore REAL NOT NULL,
                statusKilled INTEGER NOT NULL,
                statusSurvived INTEGER NOT NULL,
                statusNoCoverage INTEGER NOT NULL,
                statusCompileError INTEGER NOT NULL,
                statusRuntimeError INTEGER NOT NULL,
                statusTimeout INTEGER NOT NULL,
                statusIgnored INTEGER NOT NULL,
                statusPending INTEGER NOT NULL,
                totalMutants INTEGER NOT NULL
            );

            CREATE TABLE mutants (
                id TEXT PRIMARY KEY,
                filePath TEXT NOT NULL,
                language TEXT NOT NULL,
                startLine INTEGER NOT NULL,
                startColumn INTEGER NOT NULL,
                endLine INTEGER NOT NULL,
                endColumn INTEGER NOT NULL,
                mutator TEXT NOT NULL,
                status TEXT NOT NULL,
                description TEXT NULL,
                replacement TEXT NULL,
                statusReason TEXT NULL,
                durationMs REAL NULL,
                testsCompleted REAL NULL,
                isStatic INTEGER NULL
            );

            CREATE TABLE mutant_covered_by (
                mutantId TEXT NOT NULL,
                testId TEXT NOT NULL,
                testName TEXT NULL
            );

            CREATE TABLE mutant_killed_by (
                mutantId TEXT NOT NULL,
                testId TEXT NOT NULL,
                testName TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static void InsertSummary(SqliteConnection connection, SqliteTransaction transaction, RunSummary summary)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO summary (
                schemaVersion,
                thresholdsHigh,
                thresholdsLow,
                totalFiles,
                totalMutants,
                statusKilled,
                statusSurvived,
                statusNoCoverage,
                statusCompileError,
                statusRuntimeError,
                statusTimeout,
                statusIgnored,
                statusPending,
                mutationScorePercent,
                mutationScoreNumeratorKilled,
                mutationScoreDenominatorEffective,
                performanceSetup,
                performanceInitialRun,
                performanceMutation,
                frameworkName,
                frameworkVersion,
                systemCi
            )
            VALUES (
                $schemaVersion,
                $thresholdsHigh,
                $thresholdsLow,
                $totalFiles,
                $totalMutants,
                $statusKilled,
                $statusSurvived,
                $statusNoCoverage,
                $statusCompileError,
                $statusRuntimeError,
                $statusTimeout,
                $statusIgnored,
                $statusPending,
                $mutationScorePercent,
                $mutationScoreNumeratorKilled,
                $mutationScoreDenominatorEffective,
                $performanceSetup,
                $performanceInitialRun,
                $performanceMutation,
                $frameworkName,
                $frameworkVersion,
                $systemCi
            );
            """;

        command.Parameters.AddWithValue("$schemaVersion", summary.SchemaVersion);
        command.Parameters.AddWithValue("$thresholdsHigh", summary.Thresholds.High);
        command.Parameters.AddWithValue("$thresholdsLow", summary.Thresholds.Low);
        command.Parameters.AddWithValue("$totalFiles", summary.Totals.Files);
        command.Parameters.AddWithValue("$totalMutants", summary.Totals.Mutants);
        command.Parameters.AddWithValue("$statusKilled", summary.StatusCounts.Killed);
        command.Parameters.AddWithValue("$statusSurvived", summary.StatusCounts.Survived);
        command.Parameters.AddWithValue("$statusNoCoverage", summary.StatusCounts.NoCoverage);
        command.Parameters.AddWithValue("$statusCompileError", summary.StatusCounts.CompileError);
        command.Parameters.AddWithValue("$statusRuntimeError", summary.StatusCounts.RuntimeError);
        command.Parameters.AddWithValue("$statusTimeout", summary.StatusCounts.Timeout);
        command.Parameters.AddWithValue("$statusIgnored", summary.StatusCounts.Ignored);
        command.Parameters.AddWithValue("$statusPending", summary.StatusCounts.Pending);
        command.Parameters.AddWithValue("$mutationScorePercent", summary.MutationScore.Percent);
        command.Parameters.AddWithValue("$mutationScoreNumeratorKilled", summary.MutationScore.NumeratorKilled);
        command.Parameters.AddWithValue("$mutationScoreDenominatorEffective", summary.MutationScore.DenominatorEffective);

        command.Parameters.AddWithValue("$performanceSetup", summary.PerformanceMs?.Setup ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$performanceInitialRun", summary.PerformanceMs?.InitialRun ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$performanceMutation", summary.PerformanceMs?.Mutation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$frameworkName", summary.Framework?.Name ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$frameworkVersion", summary.Framework?.Version ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$systemCi", summary.System?.Ci ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    private static void InsertFiles(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<FileSummary> files)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO files (
                filePath,
                language,
                mutationScore,
                statusKilled,
                statusSurvived,
                statusNoCoverage,
                statusCompileError,
                statusRuntimeError,
                statusTimeout,
                statusIgnored,
                statusPending,
                totalMutants
            )
            VALUES (
                $filePath,
                $language,
                $mutationScore,
                $statusKilled,
                $statusSurvived,
                $statusNoCoverage,
                $statusCompileError,
                $statusRuntimeError,
                $statusTimeout,
                $statusIgnored,
                $statusPending,
                $totalMutants
            );
            """;

        var filePathParam = command.Parameters.Add("$filePath", SqliteType.Text);
        var languageParam = command.Parameters.Add("$language", SqliteType.Text);
        var mutationScoreParam = command.Parameters.Add("$mutationScore", SqliteType.Real);
        var statusKilledParam = command.Parameters.Add("$statusKilled", SqliteType.Integer);
        var statusSurvivedParam = command.Parameters.Add("$statusSurvived", SqliteType.Integer);
        var statusNoCoverageParam = command.Parameters.Add("$statusNoCoverage", SqliteType.Integer);
        var statusCompileErrorParam = command.Parameters.Add("$statusCompileError", SqliteType.Integer);
        var statusRuntimeErrorParam = command.Parameters.Add("$statusRuntimeError", SqliteType.Integer);
        var statusTimeoutParam = command.Parameters.Add("$statusTimeout", SqliteType.Integer);
        var statusIgnoredParam = command.Parameters.Add("$statusIgnored", SqliteType.Integer);
        var statusPendingParam = command.Parameters.Add("$statusPending", SqliteType.Integer);
        var totalMutantsParam = command.Parameters.Add("$totalMutants", SqliteType.Integer);

        foreach (var file in files)
        {
            filePathParam.Value = file.FilePath;
            languageParam.Value = file.Language;
            mutationScoreParam.Value = file.MutationScore;
            statusKilledParam.Value = file.StatusCounts.Killed;
            statusSurvivedParam.Value = file.StatusCounts.Survived;
            statusNoCoverageParam.Value = file.StatusCounts.NoCoverage;
            statusCompileErrorParam.Value = file.StatusCounts.CompileError;
            statusRuntimeErrorParam.Value = file.StatusCounts.RuntimeError;
            statusTimeoutParam.Value = file.StatusCounts.Timeout;
            statusIgnoredParam.Value = file.StatusCounts.Ignored;
            statusPendingParam.Value = file.StatusCounts.Pending;
            totalMutantsParam.Value = file.TotalMutants;

            command.ExecuteNonQuery();
        }
    }

    private static void InsertMutants(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<MutantRecord> mutants)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO mutants (
                id,
                filePath,
                language,
                startLine,
                startColumn,
                endLine,
                endColumn,
                mutator,
                status,
                description,
                replacement,
                statusReason,
                durationMs,
                testsCompleted,
                isStatic
            )
            VALUES (
                $id,
                $filePath,
                $language,
                $startLine,
                $startColumn,
                $endLine,
                $endColumn,
                $mutator,
                $status,
                $description,
                $replacement,
                $statusReason,
                $durationMs,
                $testsCompleted,
                $isStatic
            );
            """;

        var idParam = command.Parameters.Add("$id", SqliteType.Text);
        var filePathParam = command.Parameters.Add("$filePath", SqliteType.Text);
        var languageParam = command.Parameters.Add("$language", SqliteType.Text);
        var startLineParam = command.Parameters.Add("$startLine", SqliteType.Integer);
        var startColumnParam = command.Parameters.Add("$startColumn", SqliteType.Integer);
        var endLineParam = command.Parameters.Add("$endLine", SqliteType.Integer);
        var endColumnParam = command.Parameters.Add("$endColumn", SqliteType.Integer);
        var mutatorParam = command.Parameters.Add("$mutator", SqliteType.Text);
        var statusParam = command.Parameters.Add("$status", SqliteType.Text);
        var descriptionParam = command.Parameters.Add("$description", SqliteType.Text);
        var replacementParam = command.Parameters.Add("$replacement", SqliteType.Text);
        var statusReasonParam = command.Parameters.Add("$statusReason", SqliteType.Text);
        var durationParam = command.Parameters.Add("$durationMs", SqliteType.Real);
        var testsCompletedParam = command.Parameters.Add("$testsCompleted", SqliteType.Real);
        var isStaticParam = command.Parameters.Add("$isStatic", SqliteType.Integer);

        foreach (var mutant in mutants)
        {
            idParam.Value = mutant.Id;
            filePathParam.Value = mutant.FilePath;
            languageParam.Value = mutant.Language;
            startLineParam.Value = mutant.Location.StartLine;
            startColumnParam.Value = mutant.Location.StartColumn;
            endLineParam.Value = mutant.Location.EndLine;
            endColumnParam.Value = mutant.Location.EndColumn;
            mutatorParam.Value = mutant.Mutator;
            statusParam.Value = mutant.Status;
            descriptionParam.Value = mutant.Description ?? (object)DBNull.Value;
            replacementParam.Value = mutant.Replacement ?? (object)DBNull.Value;
            statusReasonParam.Value = mutant.StatusReason ?? (object)DBNull.Value;
            durationParam.Value = mutant.DurationMs ?? (object)DBNull.Value;
            testsCompletedParam.Value = mutant.TestsCompleted ?? (object)DBNull.Value;
            isStaticParam.Value = mutant.IsStatic.HasValue
                ? mutant.IsStatic.Value
                    ? 1
                    : 0
                : DBNull.Value;

            command.ExecuteNonQuery();
        }
    }

    private static void InsertMutantTests(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        IEnumerable<MutantRecord> mutants,
        Func<MutantRecord, IEnumerable<TestRef>> selector)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {tableName} (
                mutantId,
                testId,
                testName
            )
            VALUES (
                $mutantId,
                $testId,
                $testName
            );
            """;

        var mutantIdParam = command.Parameters.Add("$mutantId", SqliteType.Text);
        var testIdParam = command.Parameters.Add("$testId", SqliteType.Text);
        var testNameParam = command.Parameters.Add("$testName", SqliteType.Text);

        foreach (var mutant in mutants)
        {
            foreach (var testRef in selector(mutant))
            {
                mutantIdParam.Value = mutant.Id;
                testIdParam.Value = testRef.Id;
                testNameParam.Value = testRef.Name ?? (object)DBNull.Value;

                command.ExecuteNonQuery();
            }
        }
    }
}
